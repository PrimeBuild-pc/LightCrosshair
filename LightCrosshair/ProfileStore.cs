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
                list = DeserializeProfiles(json);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "ProfileStore.LoadAsync: primary parse");
                // Attempt recover from newest backup
                for (int i = 1; i <= 3 && list == null; i++)
                {
                    var bak = path + $".bak{i}";
                    if (File.Exists(bak))
                    {
                        try
                        {
                            json = await File.ReadAllTextAsync(bak).ConfigureAwait(false);
                            list = DeserializeProfiles(json);
                        }
                        catch (Exception backupEx) { Program.LogError(backupEx, "ProfileStore: backup load parse"); }
                    }
                }
                list ??= new();
            }

            list?.RemoveAll(p => p is null);
            foreach (var p in list ?? new List<CrosshairProfile>())
            {
                // v6 only adds an optional binding. Do not rerun legacy color/shape migrations on v5 profiles.
                if (p.SchemaVersion == 5)
                {
                    p.SchemaVersion = ProfileSchema.Current;
                    continue;
                }
                if (p.SchemaVersion < ProfileSchema.Current)
                {
                    if (p.EnumShape == default && !string.IsNullOrWhiteSpace(p.Shape))
                        p.EnumShape = ShapeNormalizer.ToEnum(p.Shape);

                    // Legacy shape migration: remove Plus variants
                    if (string.Equals(p.Shape, "Plus", StringComparison.OrdinalIgnoreCase)) p.Shape = "Cross";
                    if (string.Equals(p.Shape, "CirclePlus", StringComparison.OrdinalIgnoreCase)) p.Shape = "CircleCross";

                    // Migration to simplified color model (v2)
                    // Prefer the visible inner color for the new OuterColor; fallback to edge if inner is transparent
                    try
                    {
                        bool hasOuter = p.OuterColor.A != 0 || p.OuterColor.R != 0 || p.OuterColor.G != 0 || p.OuterColor.B != 0;
                        if (!hasOuter)
                        {
                            if (p.InnerColor.A > 0) p.OuterColor = p.InnerColor;
                            else if (p.EdgeColor.A > 0) p.OuterColor = p.EdgeColor;
                        }

                        // Migration v3: remove Plus/CirclePlus
                        if (!string.IsNullOrWhiteSpace(p.Shape))
                        {
                            if (p.Shape.Equals("Plus", StringComparison.OrdinalIgnoreCase))
                            {
                                p.Shape = "Cross";
                                p.EnumShape = CrosshairShape.Cross;
                            }
                            else if (p.Shape.Equals("CirclePlus", StringComparison.OrdinalIgnoreCase))
                            {
                                p.Shape = "CircleCross";
                                p.EnumShape = CrosshairShape.Custom; // composite
                            }
                        }

                        bool hasInnerComposite = p.InnerShapeColor.A != 0 || p.InnerShapeColor.R != 0 || p.InnerShapeColor.G != 0 || p.InnerShapeColor.B != 0;
                        if (!hasInnerComposite)
                        {
                            if (p.InnerShapeEdgeColor.A > 0) p.InnerShapeColor = p.InnerShapeEdgeColor;
                            else if (p.InnerColor.A > 0) p.InnerShapeColor = p.InnerColor;
                        }

                        // Migration v4: profile-level toggle fields were introduced in schema 4.
                        if (p.SchemaVersion < 4)
                        {
                            p.EnableCustomCrosshair = true;
                        }

                        // Migration v5: display color management values are profile-scoped.
                        // Older profiles keep HasDisplayColorProfile=false until first save from settings.
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(ex, $"ProfileStore.LoadAsync: migration for profile {p.Id}");
                    }

                    p.SchemaVersion = ProfileSchema.Current;
                }
            }
            return list ?? new List<CrosshairProfile>();
        }

        private static List<CrosshairProfile> DeserializeProfiles(string json)
        {
            using var document = JsonDocument.Parse(json, new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind == JsonValueKind.Null) return new();
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected legacy profile array.");
            var profiles = new List<CrosshairProfile>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                try
                {
                    var profile = entry.Deserialize<CrosshairProfile>(JsonOpts);
                    if (profile is null) throw new JsonException("Null legacy profile.");
                    profiles.Add(profile);
                }
                catch (JsonException ex) { Program.LogError(ex, "Skip invalid legacy profile"); }
            }
            return profiles;
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
            catch (Exception ex)
            {
                Program.LogError(ex, "ProfileStore.RotateBackups");
            }
        }
    }
}
