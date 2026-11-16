using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Runs outside Core/UI. Relay original NDJSON bytes and count successful 96px thumbnail
// replies by design id; no injected requests, test rendering shortcuts or product changes.
public sealed class LibrarySoakRelay : IDisposable
{
    private readonly CancellationTokenSource stop = new CancellationTokenSource();
    private readonly NamedPipeServerStream front;
    private readonly string target;
    private readonly object gate = new object();
    private readonly Dictionary<long, string> renders = new Dictionary<long, string>();
    private readonly HashSet<string> expected = new HashSet<string>();
    private readonly HashSet<string> completed = new HashSet<string>();
    private bool gotStore, gotPresets;
    private string failure;
    public Task Completion { get; private set; }
    public int Expected { get { lock (gate) return expected.Count; } }
    public int Rendered { get { lock (gate) return completed.Count; } }
    public bool Ready { get { lock (gate) return gotStore && gotPresets && expected.Count >= 31 && completed.IsSupersetOf(expected); } }
    public string Failure { get { lock (gate) return failure; } }
    public LibrarySoakRelay(string publicName, string coreName)
    {
        target = coreName;
        front = new NamedPipeServerStream(publicName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Completion = RunAsync();
    }
    private async Task RunAsync()
    {
        try
        {
            await front.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
            using (var back = new NamedPipeClientStream(".", target, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await back.ConnectAsync(10000, stop.Token).ConfigureAwait(false);
                Task requests = ForwardAsync(front, back, true);
                Task responses = ForwardAsync(back, front, false);
                await Task.WhenAny(requests, responses).ConfigureAwait(false);
                stop.Cancel();
                await Task.WhenAll(requests, responses).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) when (stop.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { lock (gate) failure = ex.ToString(); }
    }
    private async Task ForwardAsync(Stream source, Stream destination, bool request)
    {
        using (var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 16384, true))
        {
            while (!stop.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync(stop.Token).ConfigureAwait(false);
                if (line == null) return;
                if (request) Observe(line, true);
                byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
                await destination.WriteAsync(bytes, stop.Token).ConfigureAwait(false);
                await destination.FlushAsync(stop.Token).ConfigureAwait(false);
                if (!request) Observe(line, false);
            }
        }
    }
    private readonly Dictionary<long, string> methods = new Dictionary<long, string>();
    private void Observe(string line, bool request)
    {
        using (var document = JsonDocument.Parse(line))
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var idValue) || !idValue.TryGetInt64(out long id) || id == 0) return;
            lock (gate)
            {
                if (request)
                {
                    string method = root.GetProperty("method").GetString();
                    methods[id] = method;
                    if (method == "render" && root.TryGetProperty("payload", out var payload) &&
                        payload.GetProperty("canvasSize").GetInt32() == 96)
                        renders[id] = payload.GetProperty("design").GetProperty("id").GetString();
                    return;
                }
                if (root.GetProperty("kind").GetString() != "res" || !methods.TryGetValue(id, out string name)) return;
                methods.Remove(id);
                if (root.TryGetProperty("ok", out var ok) && !ok.GetBoolean()) { failure = line; return; }
                if (!root.TryGetProperty("payload", out var result)) return;
                if (name == "getSnapshot")
                {
                    foreach (var design in result.GetProperty("store").GetProperty("crosshairs").EnumerateArray())
                        expected.Add(design.GetProperty("id").GetString());
                    gotStore = true;
                }
                else if (name == "getPresets")
                {
                    foreach (var preset in result.EnumerateArray()) expected.Add(preset.GetProperty("design").GetProperty("id").GetString());
                    gotPresets = true;
                }
                else if (renders.TryGetValue(id, out string designId))
                {
                    string png = result.GetProperty("pngBase64").GetString();
                    if (String.IsNullOrEmpty(png)) { failure = "Empty thumbnail"; return; }
                    renders.Remove(id); completed.Add(designId);
                }
            }
        }
    }
    public void Dispose()
    {
        stop.Cancel(); front.Dispose();
        Completion.GetAwaiter().GetResult(); stop.Dispose();
    }
}
