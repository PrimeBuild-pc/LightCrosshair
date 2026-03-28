using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace LightCrosshair
{
    internal enum AppTheme { Dark, Light }

    internal sealed class AppPreferences
    {
        public AppTheme Theme { get; set; } = AppTheme.Dark;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 1320;
        public int WindowHeight { get; set; } = 600;
        public bool FirstRunDone { get; set; } = false;
        public bool OverlayVisible { get; set; } = true;
        public string LastProfileId { get; set; } = string.Empty;
    }

    internal static class PreferencesStore
    {
        private static readonly string PrefsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LightCrosshair", "prefs.json");

        public static AppPreferences Load()
        {
            try
            {
                if (File.Exists(PrefsPath))
                {
                    var json = File.ReadAllText(PrefsPath);
                    var prefs = JsonSerializer.Deserialize<AppPreferences>(json);
                    if (prefs != null) return prefs;
                }
            }
            catch { }
            return new AppPreferences();
        }

        public static void Save(AppPreferences prefs)
        {
            try
            {
                var dir = Path.GetDirectoryName(PrefsPath);
                if (dir != null) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                var tmpPath = PrefsPath + ".tmp";
                
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, PrefsPath, true);
            }
            catch { }
        }
    }
}

