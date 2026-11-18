using System.Buffers;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class BoundedLineReader(Stream stream)
{
    internal const int MaxLineBytes = 8 * 1024 * 1024;
    private readonly byte[] _buffer = new byte[16384];
    private int _offset, _count;
    public async Task<string?> ReadAsync(CancellationToken token)
    {
        using var line = new MemoryStream();
        while (true)
        {
            if (_offset == _count)
            {
                _count = await stream.ReadAsync(_buffer, token).ConfigureAwait(false); _offset = 0;
                if (_count == 0) return line.Length == 0 ? null : throw new InvalidDataException("Unterminated line.");
            }
            int end = Array.IndexOf(_buffer, (byte)'\n', _offset, _count - _offset);
            int length = end < 0 ? _count - _offset : end - _offset;
            if (line.Length + length > MaxLineBytes) throw new InvalidDataException("Line too long.");
            line.Write(_buffer, _offset, length); _offset += length + (end < 0 ? 0 : 1);
            if (end >= 0) return new UTF8Encoding(false, true).GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r');
        }
    }
}

internal sealed class PipeServer : IDisposable
{
    internal uint ClientProcessId { get; private set; }
    internal CancellationToken ConnectionToken => _connection?.Token ?? new CancellationToken(true);
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    private readonly string _name;
    private readonly Func<IpcEnvelope, Task<object?>> _dispatch;
    private readonly Func<Task> _disconnected;
    private readonly CancellationTokenSource _stop = new();
    private Channel<byte[]>? _outbound;
    private CancellationTokenSource? _connection;
    private int _connected;
    public bool IsConnected => Volatile.Read(ref _connected) != 0;
    internal Task Completion { get; }
    public PipeServer(string name, Func<IpcEnvelope, Task<object?>> dispatch, Func<Task> disconnected)
    {
        _name = name; _dispatch = dispatch; _disconnected = disconnected;
        Completion = Task.Run(async () => { try { await RunAsync().ConfigureAwait(false); } finally { _stop.Dispose(); } });
    }
    public void Publish(string method, object? payload)
    {
        if (!IsConnected) return;
        byte[] frame = Frame(new() { Kind = IpcKinds.Event, Id = 0, Method = method }, payload);
        var queue = Volatile.Read(ref _outbound);
        if (queue is not null && !queue.Writer.TryWrite(frame)) Cancel(_connection);
    }
    internal static JsonElement? Element(object? value) => value is null ? null : JsonSerializer.SerializeToElement(value, ContractJson.Options);
    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                ClientProcessId = GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint clientPid) ? clientPid : 0;
                using var connection = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                _connection = connection;
                var outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
                Volatile.Write(ref _outbound, outbound);
                Task write = WriteAsync(pipe, outbound.Reader, connection);
                try { await ReadAsync(pipe, outbound.Writer, connection.Token).ConfigureAwait(false); }
                finally
                {
                    outbound.Writer.TryComplete(); connection.Cancel();
                    try { await write.ConfigureAwait(false); } catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or JsonException or InvalidDataException or DecoderFallbackException)
            { if (!_stop.IsCancellationRequested) Program.WriteLog("IPC connection ended"); }
            finally
            {
                Volatile.Write(ref _connected, 0); Volatile.Write(ref _outbound, null); _connection = null; ClientProcessId = 0;
                try { await _disconnected().ConfigureAwait(false); }
                catch (Exception ex) { Program.LogError(ex, "IPC disconnect"); }
            }
        }
    }
    private static async Task WriteAsync(Stream stream, ChannelReader<byte[]> queue, CancellationTokenSource connection)
    {
        try
        {
            await foreach (byte[] bytes in queue.ReadAllAsync(connection.Token).ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, connection.Token).ConfigureAwait(false);
                await stream.FlushAsync(connection.Token).ConfigureAwait(false);
            }
        }
        finally { connection.Cancel(); }
    }
    private async Task ReadAsync(Stream stream, ChannelWriter<byte[]> outbound, CancellationToken token)
    {
        var reader = new BoundedLineReader(stream);
        bool hello = false;
        while (!token.IsCancellationRequested)
        {
            string? line = await reader.ReadAsync(token).ConfigureAwait(false);
            if (line is null) return;
            IpcEnvelope request = JsonSerializer.Deserialize<IpcEnvelope>(line, ContractJson.Options) ?? throw new InvalidDataException();
            var response = new IpcEnvelope { Kind = IpcKinds.Response, Id = request.Id };
            bool close = false;
            object? result = null;
            try
            {
                if (request.Kind != IpcKinds.Request || request.Id <= 0 || string.IsNullOrEmpty(request.Method) || request.Method.Length > 64) throw new CoreException(IpcErrorCodes.BadRequest, "Invalid request envelope.");
                if (!hello)
                {
                    close = true;
                    if (request.Method != IpcMethods.Hello) throw new CoreException(IpcErrorCodes.BadRequest, "hello must be first.");
                    var payload = request.Payload;
                    if (payload?.ValueKind != JsonValueKind.Object || !payload.Value.TryGetProperty("protocolVersion", out var version) || !version.TryGetInt32(out int number))
                        throw new CoreException(IpcErrorCodes.BadRequest, "Missing protocol version.");
                    if (number != ContractInfo.ProtocolVersion) throw new CoreException(IpcErrorCodes.ProtocolMismatch, "Protocol version mismatch.");
                    var greeting = payload.Value.Deserialize<HelloRequest>(ContractJson.Options);
                    if (greeting?.Client is null || greeting.Client.Length > 128) throw new CoreException(IpcErrorCodes.BadRequest, "Invalid client.");
                    hello = true; close = false; Volatile.Write(ref _connected, 1);
                    result = new HelloResponse { CoreVersion = "2.0.1" };
                }
                else if (request.Method == IpcMethods.Hello) throw new CoreException(IpcErrorCodes.BadRequest, "Already initialized.");
                else result = await _dispatch(request).ConfigureAwait(false);
            }
            catch (CoreException ex) { response.Ok = false; response.Error = new() { Code = ex.Code, Message = ex.Message }; }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
            { response.Ok = false; response.Error = new() { Code = IpcErrorCodes.BadRequest, Message = "Malformed request." }; }
            catch (Exception ex)
            { Program.LogError(ex, "IPC request"); response.Ok = false; response.Error = new() { Code = IpcErrorCodes.Internal, Message = "Request failed." }; }
            if (close)
            {
                // A mismatch response must reach the client before this stream is closed.
                byte[] bytes = Frame(response, result);
                await stream.WriteAsync(bytes, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); return;
            }
            await outbound.WriteAsync(Frame(response, result), token).ConfigureAwait(false);
        }
    }
    private static byte[] Frame(IpcEnvelope envelope, object? payload)
    {
        // Serialize once to UTF-8. Avoid a payload JsonDocument plus a full UTF-16
        // envelope string for every render/snapshot response.
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", envelope.Kind); writer.WriteNumber("id", envelope.Id);
            if (envelope.Method is not null) writer.WriteString("method", envelope.Method);
            if (envelope.Kind == IpcKinds.Response) writer.WriteBoolean("ok", envelope.Ok);
            if (payload is not null) { writer.WritePropertyName("payload"); JsonSerializer.Serialize(writer, payload, ContractJson.Options); }
            if (envelope.Error is not null) { writer.WritePropertyName("error"); JsonSerializer.Serialize(writer, envelope.Error, ContractJson.Options); }
            writer.WriteEndObject();
        }
        if (buffer.WrittenCount > BoundedLineReader.MaxLineBytes) throw new InvalidDataException("Response too large.");
        buffer.GetSpan(1)[0] = (byte)'\n'; buffer.Advance(1);
        return buffer.WrittenSpan.ToArray();
    }
    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { } // A simultaneous disconnect already completed it.
    }
    public void Dispose() { Cancel(_stop); Cancel(_connection); }
}
