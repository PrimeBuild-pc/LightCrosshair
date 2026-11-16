using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Services;
using Xunit;

namespace LightCrosshair.UI.Tests;

public class PipeClientTests
{
    [Fact]
    public async Task HelloIsFirstAndResponsesMatchById()
    {
        var name = "lc-ui-test-" + Guid.NewGuid().ToString("N");
        await using var server = Server(name);
        await using var client = Client(name, TimeSpan.FromSeconds(2));
        var wait = server.WaitForConnectionAsync();
        var connect = client.ConnectAsync();
        await wait.WaitAsync(TimeSpan.FromSeconds(3));
        var hello = await ReadAsync(server).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(IpcMethods.Hello, hello.Method);
        await ReplyAsync(server, hello, new HelloResponse { ProtocolVersion = ContractInfo.ProtocolVersion, CoreVersion = "test" });
        var snapshotRequest = await ReadAsync(server).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(IpcMethods.GetSnapshot, snapshotRequest.Method);
        await ReplyAsync(server, snapshotRequest, EmptySnapshot());
        await connect.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(CoreLinkState.Connected, client.State);
        Assert.NotNull(client.Snapshot);

        var presetsTask = client.GetPresetsAsync();
        var processesTask = client.ListProcessesAsync();
        var first = await ReadAsync(server).WaitAsync(TimeSpan.FromSeconds(3));
        var second = await ReadAsync(server).WaitAsync(TimeSpan.FromSeconds(3));
        await ReplyAsync(server, second, PayloadFor(second.Method));
        await ReplyAsync(server, first, PayloadFor(first.Method));
        var presets = await presetsTask;
        var processes = await processesTask;
        Assert.Equal("dot", presets[0].Design.Name);
        Assert.Equal("game.exe", processes[0].ProcessName);
    }

    [Fact]
    public async Task RequestTimesOutWhenCoreStaysSilent()
    {
        var name = "lc-ui-test-" + Guid.NewGuid().ToString("N");
        await using var server = Server(name);
        await using var client = Client(name, TimeSpan.FromMilliseconds(300));
        var wait = server.WaitForConnectionAsync();
        var connect = client.ConnectAsync();
        await wait.WaitAsync(TimeSpan.FromSeconds(3));
        await HandshakeAsync(server);
        await connect.WaitAsync(TimeSpan.FromSeconds(3));
        var drained = ReadAsync(server);
        var error = await Assert.ThrowsAsync<CoreException>(() => client.GetPresetsAsync());
        _ = drained;
        Assert.Equal("timeout", error.Code);
        Assert.Equal(error.Code, error.Message);
    }

    [Fact]
    public async Task OversizedLineDropsTheConnection()
    {
        var name = "lc-ui-test-" + Guid.NewGuid().ToString("N");
        await using var server = Server(name);
        await using var client = new PipeCoreClient(new PipeCoreClientOptions
        {
            PipeName = name,
            StartCore = _ => Task.CompletedTask,
            Marshaler = new InlineMarshaler(),
            RequestTimeout = TimeSpan.FromSeconds(2),
            ConnectTimeout = TimeSpan.FromSeconds(3),
            MaxLineBytes = 32 * 1024,
        });
        var wait = server.WaitForConnectionAsync();
        var connect = client.ConnectAsync();
        await wait.WaitAsync(TimeSpan.FromSeconds(3));
        await HandshakeAsync(server);
        await connect.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(CoreLinkState.Connected, client.State);
        var pending = client.GetPresetsAsync();
        try
        {
            _ = await ReadAsync(server).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException)
        {
            var detail = pending.IsCompleted ? pending.Exception?.GetBaseException().ToString() : "request still pending";
            throw new TimeoutException("Core never sent the request. " + detail);
        }
        var junk = Encoding.UTF8.GetBytes(new string('A', 40 * 1024));
        try
        {
            await server.WriteAsync(junk);
            await server.FlushAsync();
        }
        catch (IOException) { /* the client drops the pipe once the line is too long */ }
        var error = await Assert.ThrowsAsync<CoreException>(() => pending);
        Assert.Equal("disconnected", error.Code);
    }

    private static NamedPipeServerStream Server(string name) => new(
        name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private static PipeCoreClient Client(string name, TimeSpan requestTimeout) => new(new PipeCoreClientOptions
    {
        PipeName = name,
        StartCore = _ => Task.CompletedTask,
        Marshaler = new InlineMarshaler(),
        RequestTimeout = requestTimeout,
        ConnectTimeout = TimeSpan.FromSeconds(3),
    });

    private static async Task HandshakeAsync(Stream stream)
    {
        var hello = await ReadAsync(stream).WaitAsync(TimeSpan.FromSeconds(3));
        await ReplyAsync(stream, hello, new HelloResponse { ProtocolVersion = ContractInfo.ProtocolVersion, CoreVersion = "test" });
        var snapshot = await ReadAsync(stream).WaitAsync(TimeSpan.FromSeconds(3));
        await ReplyAsync(stream, snapshot, EmptySnapshot());
    }

    private static Snapshot EmptySnapshot() => new()
    {
        Store = new StoreDocument { Settings = new AppSettings { Hotkeys = new HotkeySettings() } },
        Runtime = new RuntimeState(),
    };

    private static object PayloadFor(string? method) => method == IpcMethods.ListProcesses
        ? new List<ProcessEntry> { new() { ProcessName = "game.exe", WindowTitle = "Game" } }
        : new List<PresetEntry> { new() { Tags = ["dot"], Design = new CrosshairDesign { Name = "dot" } } };

    private static async Task<IpcEnvelope> ReadAsync(Stream stream)
    {
        var line = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) throw new EndOfStreamException();
            if (buffer[0] == (byte)'\n')
            {
                var text = Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r');
                return JsonSerializer.Deserialize<IpcEnvelope>(text, ContractJson.Options) ?? throw new InvalidOperationException(text);
            }
            line.WriteByte(buffer[0]);
        }
    }

    private static async Task ReplyAsync(Stream stream, IpcEnvelope request, object payload)
    {
        var envelope = new IpcEnvelope
        {
            Kind = IpcKinds.Response,
            Id = request.Id,
            Method = request.Method,
            Ok = true,
            Payload = JsonSerializer.SerializeToElement(payload, payload.GetType(), ContractJson.Options),
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, ContractJson.Options) + "\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}
