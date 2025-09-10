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
        public int WindowWidth { get; set; } = 600;
        public int WindowHeight { get; set; } = 500;
        public bool FirstRunDone { get; set; } = false;
    }

    internal static class PreferencesStore
    {
        private static readonly string PrefsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prefs.json");

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
                var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PrefsPath, json);
            }
            catch { }
        }
    }
}

