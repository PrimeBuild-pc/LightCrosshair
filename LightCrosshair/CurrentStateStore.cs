using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LightCrosshair
{
    internal sealed class CurrentStateSnapshot
    {
        public CrosshairProfile? CurrentProfile { get; set; }
    }

    internal static class CurrentStateStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() }
        };

        private static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LightCrosshair",
            "current_state.json");

        public static async Task<CurrentStateSnapshot?> LoadAsync()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(StatePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<CurrentStateSnapshot>(json, JsonOpts);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "CurrentStateStore.LoadAsync");
                return null;
            }
        }

        public static async Task SaveAtomicAsync(CurrentStateSnapshot snapshot)
        {
            try
            {
                var dir = Path.GetDirectoryName(StatePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(snapshot, JsonOpts);
                string tmp = StatePath + ".tmp";
                await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
                File.Move(tmp, StatePath, true);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "CurrentStateStore.SaveAtomicAsync");
            }
        }
    }
}
