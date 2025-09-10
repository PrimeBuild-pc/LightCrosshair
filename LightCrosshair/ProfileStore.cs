using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LightCrosshair
{
    public static class ProfileStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LightCrosshair", "profiles.json");

        public static async Task<List<CrosshairProfile>> LoadAsync(string? path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return new();

            string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            List<CrosshairProfile>? list = null;
            try
            {
                list = JsonSerializer.Deserialize<List<CrosshairProfile>>(json, JsonOpts);
            }
            catch
            {
                // Attempt recover from newest backup
                for (int i = 1; i <= 3 && list == null; i++)
                {
                    var bak = path + $".bak{i}";
                    if (File.Exists(bak))
                    {
                        try
                        {
                            json = await File.ReadAllTextAsync(bak).ConfigureAwait(false);
                            list = JsonSerializer.Deserialize<List<CrosshairProfile>>(json, JsonOpts);
                        }
                        catch { }
                    }
                }
                list ??= new();
            }

            foreach (var p in list ?? new List<CrosshairProfile>())
            {
                if (p.SchemaVersion < ProfileSchema.Current)
                {
                    if (p.EnumShape == default && !string.IsNullOrWhiteSpace(p.Shape))
                        p.EnumShape = ShapeNormalizer.ToEnum(p.Shape);
                    p.SchemaVersion = ProfileSchema.Current;
                }
            }
            return list ?? new List<CrosshairProfile>();
        }

        public static async Task SaveAtomicAsync(IEnumerable<CrosshairProfile> profiles, string? path = null)
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = JsonSerializer.Serialize(profiles, JsonOpts);
            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);

            RotateBackups(path);
            File.Move(tmp, path, true);
        }

        private static void RotateBackups(string path)
        {
            string b1 = path + ".bak1", b2 = path + ".bak2", b3 = path + ".bak3";
            try
            {
                if (File.Exists(b3)) File.Delete(b3);
                if (File.Exists(b2)) File.Move(b2, b3);
                if (File.Exists(b1)) File.Move(b1, b2);
                if (File.Exists(path)) File.Copy(path, b1, true);
            }
            catch { /* swallow backup rotation issues */ }
        }
    }
}
