using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Diagnostics;

namespace LightCrosshair
{
    // Enum for normalized shapes (step A)
    public enum CrosshairShape { Dot, Cross, CrossOutlined, Circle, CircleOutlined, T, X, Box, GapCross, Custom }

    public class CrosshairConfig : IDisposable
    {
        private static CrosshairConfig? _instance;
        public static CrosshairConfig Instance => _instance ??= new CrosshairConfig();
        [ThreadStatic]
        private static bool _isDeserializing;

        // File path for settings
        private readonly string _configFilePath;

        // Properties for crosshair configuration
        [JsonIgnore]
        public Color CrosshairColor { get; set; } = Color.Lime;

        [JsonPropertyName("CrosshairColorSerialized")]
        public string CrosshairColorSerialized
        {
            get => $"{CrosshairColor.R},{CrosshairColor.G},{CrosshairColor.B}";
            set
            {
                try
                {
                    var parts = value.Split(',');
                    if (parts.Length == 3 && 
                        int.TryParse(parts[0], out int r) && 
                        int.TryParse(parts[1], out int g) && 
                        int.TryParse(parts[2], out int b))
                    {
                        CrosshairColor = Color.FromArgb(r, g, b);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing color: {ex.Message}");
                    CrosshairColor = Color.Lime; // Default to lime if parsing fails
                }
            }
        }

        public int CrosshairSize { get; set; } = 20;
        public int CrosshairThickness { get; set; } = 2;
        public string CrosshairStyle { get; set; } = "Cross";
        public bool Visible { get; set; } = true;
        public string TargetProcessName { get; set; } = "";
        
        // Hotkey settings
        public Keys HotkeyKey { get; set; } = Keys.X;
        public bool HotkeyUseAlt { get; set; } = true;
        public bool HotkeyUseControl { get; set; } = false;
        public bool HotkeyUseShift { get; set; } = false;
        public bool HotkeyUseWin { get; set; } = false;

        public Keys CycleProfileHotkeyKey { get; set; } = Keys.C;
        public bool CycleProfileHotkeyUseAlt { get; set; } = true;
        public bool CycleProfileHotkeyUseControl { get; set; } = false;
        public bool CycleProfileHotkeyUseShift { get; set; } = false;
        public bool CycleProfileHotkeyUseWin { get; set; } = false;

        public Keys CycleProfilePrevHotkeyKey { get; set; } = Keys.V;
        public bool CycleProfilePrevHotkeyUseAlt { get; set; } = true;
        public bool CycleProfilePrevHotkeyUseControl { get; set; } = false;
        public bool CycleProfilePrevHotkeyUseShift { get; set; } = false;
        public bool CycleProfilePrevHotkeyUseWin { get; set; } = false;

        public Keys SettingsWindowHotkeyKey { get; set; } = Keys.L;
        public bool SettingsWindowHotkeyUseAlt { get; set; } = true;
        public bool SettingsWindowHotkeyUseControl { get; set; } = false;
        public bool SettingsWindowHotkeyUseShift { get; set; } = false;
        public bool SettingsWindowHotkeyUseWin { get; set; } = false;

        // Display (Gamma/Vibrance) Settings
        public bool EnableGammaOverride { get; set; } = false;
        public int GammaValue { get; set; } = 100; // 100 = default
        public int ContrastValue { get; set; } = 100; // 100 = default
        public int BrightnessValue { get; set; } = 100; // 100 = default
        public int VibranceValue { get; set; } = 50; // 50 = default

        // FPS Overlay Settings
        public bool EnableFpsOverlay { get; set; } = false;
        public int FpsOverlayX { get; set; } = 10;
        public int FpsOverlayY { get; set; } = 10;
        public bool ShowFrametimeGraph { get; set; } = true;
        public const int GraphRefreshRateDefaultMs = 66;
        public const int GraphTimeWindowDefaultMs = 2000;

        private int _graphRefreshRateMs = GraphRefreshRateDefaultMs;
        private int _graphTimeWindowMs = GraphTimeWindowDefaultMs;

        public int GraphRefreshRateMs
        {
            get => _graphRefreshRateMs;
            set => _graphRefreshRateMs = NormalizeGraphRefreshRatePreset(value);
        }

        public int GraphTimeWindowMs
        {
            get => _graphTimeWindowMs;
            set => _graphTimeWindowMs = NormalizeGraphTimeWindowPreset(value);
        }
        public bool Show1PercentLows { get; set; } = true;
        public bool ShowGenFrames { get; set; } = true;
        public string FpsOverlayColorSerialized { get; set; } = "255,255,255";
        public string FpsOverlayBgColorSerialized { get; set; } = "0,0,0";
        private int _fpsOverlayScale = 100;
        public int FpsOverlayScale
        {
            get => _fpsOverlayScale;
            set => _fpsOverlayScale = NormalizeFpsOverlayScale(value);
        }

    // Rendering flags
    public bool AntiAlias { get; set; } = false; // Used by AA toggle (step B)

        // Event triggered when settings are changed
        public event EventHandler? SettingsChanged;
        public event EventHandler? HotkeysRegistrationRequested;

        private bool _disposed = false;

        public CrosshairConfig()
        {
            try
            {
                // Initialize the config file path
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LightCrosshair");
                
                // Create the directory if it doesn't exist
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }
                
                _configFilePath = Path.Combine(appDataPath, "crosshair_settings.json");
                
                // Load settings
                if (!_isDeserializing)
                {
                    LoadSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CrosshairConfig constructor: {ex.Message}");
                // Use default values if initialization fails
                _configFilePath = "crosshair_settings.json";
            }
        }

        public void SaveSettings()
        {
            if (_disposed) return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                string jsonString = JsonSerializer.Serialize(this, options);
                string tmpPath = _configFilePath + ".tmp";
                
                File.WriteAllText(tmpPath, jsonString);
                File.Move(tmpPath, _configFilePath, true);
                
                OnSettingsChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                Program.LogError(ex, "CrosshairConfig.SaveSettings");
            }
        }

        public void LoadSettings()
        {
            if (_disposed) return;

            try
            {
                if (File.Exists(_configFilePath))
                {
                    string jsonString = File.ReadAllText(_configFilePath);
                    CrosshairConfig? loadedConfig;
                    bool previous = _isDeserializing;
                    try
                    {
                        _isDeserializing = true;
                        loadedConfig = JsonSerializer.Deserialize<CrosshairConfig>(jsonString);
                    }
                    finally
                    {
                        _isDeserializing = previous;
                    }
                    
                    if (loadedConfig != null)
                    {
                        // Copy properties from loaded config
                        CrosshairColorSerialized = loadedConfig.CrosshairColorSerialized;
                        CrosshairSize = Math.Max(10, Math.Min(100, loadedConfig.CrosshairSize));
                        CrosshairThickness = Math.Max(1, Math.Min(10, loadedConfig.CrosshairThickness));
                        CrosshairStyle = loadedConfig.CrosshairStyle ?? "Cross";
                        Visible = loadedConfig.Visible;
                        TargetProcessName = loadedConfig.TargetProcessName ?? string.Empty;
                        HotkeyKey = loadedConfig.HotkeyKey;
                        HotkeyUseAlt = loadedConfig.HotkeyUseAlt;
                        HotkeyUseControl = loadedConfig.HotkeyUseControl;
                        HotkeyUseShift = loadedConfig.HotkeyUseShift;
                        HotkeyUseWin = loadedConfig.HotkeyUseWin;

                        CycleProfileHotkeyKey = loadedConfig.CycleProfileHotkeyKey;
                        CycleProfileHotkeyUseAlt = loadedConfig.CycleProfileHotkeyUseAlt;
                        CycleProfileHotkeyUseControl = loadedConfig.CycleProfileHotkeyUseControl;
                        CycleProfileHotkeyUseShift = loadedConfig.CycleProfileHotkeyUseShift;
                        CycleProfileHotkeyUseWin = loadedConfig.CycleProfileHotkeyUseWin;

                        CycleProfilePrevHotkeyKey = loadedConfig.CycleProfilePrevHotkeyKey;
                        CycleProfilePrevHotkeyUseAlt = loadedConfig.CycleProfilePrevHotkeyUseAlt;
                        CycleProfilePrevHotkeyUseControl = loadedConfig.CycleProfilePrevHotkeyUseControl;
                        CycleProfilePrevHotkeyUseShift = loadedConfig.CycleProfilePrevHotkeyUseShift;
                        CycleProfilePrevHotkeyUseWin = loadedConfig.CycleProfilePrevHotkeyUseWin;

                        MigrateLegacyCycleBackHotkeyDefaults();

                        SettingsWindowHotkeyKey = loadedConfig.SettingsWindowHotkeyKey;
                        SettingsWindowHotkeyUseAlt = loadedConfig.SettingsWindowHotkeyUseAlt;
                        SettingsWindowHotkeyUseControl = loadedConfig.SettingsWindowHotkeyUseControl;
                        SettingsWindowHotkeyUseShift = loadedConfig.SettingsWindowHotkeyUseShift;
                        SettingsWindowHotkeyUseWin = loadedConfig.SettingsWindowHotkeyUseWin;

                        EnableGammaOverride = loadedConfig.EnableGammaOverride;
                        GammaValue = Math.Clamp(loadedConfig.GammaValue, 50, 150);
                        ContrastValue = Math.Clamp(loadedConfig.ContrastValue, 50, 150);
                        BrightnessValue = loadedConfig.BrightnessValue <= 0
                            ? 100
                            : Math.Clamp(loadedConfig.BrightnessValue, 50, 150);
                        VibranceValue = Math.Clamp(loadedConfig.VibranceValue, 0, 100);

                        EnableFpsOverlay = loadedConfig.EnableFpsOverlay;
                        FpsOverlayX = loadedConfig.FpsOverlayX;
                        FpsOverlayY = loadedConfig.FpsOverlayY;
                        ShowFrametimeGraph = loadedConfig.ShowFrametimeGraph;
                        GraphRefreshRateMs = loadedConfig.GraphRefreshRateMs;
                        GraphTimeWindowMs = loadedConfig.GraphTimeWindowMs;
                        Show1PercentLows = loadedConfig.Show1PercentLows;
                        ShowGenFrames = loadedConfig.ShowGenFrames;
                        FpsOverlayColorSerialized = loadedConfig.FpsOverlayColorSerialized ?? "255,255,255";
                        FpsOverlayBgColorSerialized = loadedConfig.FpsOverlayBgColorSerialized ?? "0,0,0";
                        FpsOverlayScale = loadedConfig.FpsOverlayScale;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Don't show message box here as it might be called during initialization
            }
        }

        public void ReRegisterHotkeys()
        {
            try
            {
                HotkeysRegistrationRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting hotkey re-registration: {ex.Message}");
            }
        }

        protected virtual void OnSettingsChanged()
        {
            if (!_disposed)
            {
                try
                {
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnSettingsChanged: {ex.Message}");
                }
            }
        }

        public static int NormalizeGraphRefreshRatePreset(int value)
        {
            if (value <= 0) return GraphRefreshRateDefaultMs;
            if (value <= 49) return 33;
            if (value <= 83) return 66;
            return 100;
        }

        public static int NormalizeGraphTimeWindowPreset(int value)
        {
            if (value <= 0) return GraphTimeWindowDefaultMs;
            if (value <= 1750) return 1500;
            if (value <= 2500) return 2000;
            return 3000;
        }

        public static int NormalizeFpsOverlayScale(int value)
        {
            if (value <= 0) return 100;
            return Math.Clamp(value, 50, 300);
        }

        private void MigrateLegacyCycleBackHotkeyDefaults()
        {
            bool hasLegacyBackCombo =
                CycleProfilePrevHotkeyKey == Keys.Left &&
                !CycleProfilePrevHotkeyUseAlt &&
                CycleProfilePrevHotkeyUseControl &&
                CycleProfilePrevHotkeyUseShift &&
                !CycleProfilePrevHotkeyUseWin;

            if (!hasLegacyBackCombo)
            {
                return;
            }

            CycleProfilePrevHotkeyKey = Keys.V;
            CycleProfilePrevHotkeyUseAlt = true;
            CycleProfilePrevHotkeyUseControl = false;
            CycleProfilePrevHotkeyUseShift = false;
            CycleProfilePrevHotkeyUseWin = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        SaveSettings(); // Save settings before disposing
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing CrosshairConfig: {ex.Message}");
                    }
                }
                _disposed = true;
            }
        }
    }
}

