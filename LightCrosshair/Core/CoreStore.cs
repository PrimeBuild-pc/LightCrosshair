using System.Text.Json;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class CoreStore(string directory)
{
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly object _pendingGate = new();
    private byte[]? _pending;
    private Task? _pendingWrite;
    private long _revision;
    private DateTimeOffset _lastBackup;
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string ImagesDirectory => Path.Combine(DirectoryPath, "images");
    private string StorePath => Path.Combine(DirectoryPath, "store.json");
    public bool Recovered { get; private set; }

    public Exception? LoadError { get; private set; }
    public async Task<StoreDocument> LoadAsync(string? legacyDirectory = null)
    {
        try { return await LoadPersistentAsync(legacyDirectory).ConfigureAwait(false); }
        catch (Exception ex)
        {
            LoadError = ex;
            Program.LogError(ex, "Load store; using temporary defaults", DirectoryPath);
            return LegacyMigration.Defaults();
        }
    }
    private async Task<StoreDocument> LoadPersistentAsync(string? legacyDirectory)
    {
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "backups"));
        if (File.Exists(StorePath))
        {
            try { return await ReadAsync(StorePath).ConfigureAwait(false); }
            catch (Exception ex) when (ex is JsonException or CoreException or IOException or ArgumentException or NullReferenceException)
            {
                File.Move(StorePath, Path.Combine(DirectoryPath, $"store.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"));
                Recovered = true; Program.WriteLog("Store recovery", DirectoryPath);
            }
        }
        StoreDocument? document = null;
        foreach (string backup in Directory.GetFiles(Path.Combine(DirectoryPath, "backups"), "store-*.json").OrderByDescending(Path.GetFileName))
        {
            try { document = await ReadAsync(backup).ConfigureAwait(false); Recovered = true; break; }
            catch (Exception ex) when (ex is JsonException or CoreException or IOException or ArgumentException or NullReferenceException) { }
        }
        document ??= await LegacyMigration.LoadAsync(legacyDirectory ?? Path.GetDirectoryName(DirectoryPath)!, ImagesDirectory).ConfigureAwait(false);
        await SaveAsync(document).ConfigureAwait(false);
        return document;
    }
    private async Task<StoreDocument> ReadAsync(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw Validation.Invalid("Store too large.");
        return Validation.Store(JsonSerializer.Deserialize<StoreDocument>(await File.ReadAllTextAsync(path).ConfigureAwait(false), ContractJson.Options), ImagesDirectory);
    }
    public Task SaveAsync(StoreDocument document)
    {
        var valid = Validation.Store(Validation.Copy(document), ImagesDirectory);
        // Reserve room for RuntimeState and the envelope inside the protocol's 8 MiB limit.
        // A successful save must remain readable and transferable on the next launch.
        if (JsonSerializer.SerializeToUtf8Bytes(valid, ContractJson.Options).Length > 7 * 1024 * 1024) throw Validation.Invalid("Library exceeds the transport limit.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(valid, ContractJson.IndentedOptions);
        if (bytes.Length > 32 * 1024 * 1024) throw Validation.Invalid("Library exceeds the storage limit.");
        if (LoadError is not null) return Task.CompletedTask;
        lock (_pendingGate)
        {
            _pending = bytes; _revision++;
            return _pendingWrite ??= FlushPendingAsync();
        }
    }
    private async Task FlushPendingAsync()
    {
        try
        {
            while (true)
            {
                long revision;
                lock (_pendingGate) revision = _revision;
                await Task.Delay(300).ConfigureAwait(false);
                byte[] bytes;
                lock (_pendingGate) { if (revision != _revision) continue; bytes = _pending!; }
                await _writer.WaitAsync().ConfigureAwait(false);
                string temp = StorePath + ".tmp";
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    { await output.WriteAsync(bytes).ConfigureAwait(false); await output.FlushAsync().ConfigureAwait(false); output.Flush(true); }
                    if (File.Exists(StorePath))
                    {
                        BackupPrevious();
                        File.Replace(temp, StorePath, null);
                    }
                    else File.Move(temp, StorePath);
                }
                finally { _writer.Release(); }
                lock (_pendingGate)
                {
                    if (revision != _revision) continue;
                    _pending = null; _pendingWrite = null; return;
                }
            }
        }
        catch { lock (_pendingGate) _pendingWrite = null; throw; }
    }
    private void BackupPrevious()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastBackup < TimeSpan.FromHours(1)) return;
        string backups = Path.Combine(DirectoryPath, "backups");
        Directory.CreateDirectory(backups);
        var existing = Directory.GetFiles(backups, "store-*.json").OrderByDescending(Path.GetFileName).ToArray();
        if (existing.Length > 0 && now - File.GetLastWriteTimeUtc(existing[0]) < TimeSpan.FromHours(1)) return;
        string backup = Path.Combine(backups, $"store-{now:yyyyMMdd-HHmmss-fffffff}.json");
        File.Copy(StorePath, backup);
        File.SetLastWriteTimeUtc(backup, now.UtcDateTime);
        _lastBackup = now;
        foreach (var old in Directory.GetFiles(backups, "store-*.json").OrderByDescending(Path.GetFileName).Skip(10)) File.Delete(old);
    }
}
