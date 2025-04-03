using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using System.Diagnostics;

namespace LightCrosshair
{
    public class CrosshairConfig : IDisposable
    {
        // Constants for hotkey registration
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private const int HOTKEY_ID = 9000;

        // Windows API imports for hotkey registration
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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
        
        // Hotkey settings
        public Keys HotkeyKey { get; set; } = Keys.X;
        public bool HotkeyUseAlt { get; set; } = true;
        public bool HotkeyUseControl { get; set; } = false;
        public bool HotkeyUseShift { get; set; } = false;
        public bool HotkeyUseWin { get; set; } = false;

        // Event triggered when settings are changed
        public event EventHandler? SettingsChanged;

        private Form? _ownerForm;
        private bool _hotkeyRegistered = false;
        private bool _disposed = false;

        public CrosshairConfig()
        {
            try
            {
                // Initialize the config file path
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LightCrosshair");
                
                // Create the directory if it doesn't exist
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }
                
                _configFilePath = Path.Combine(appDataPath, "crosshair_settings.json");
                
                // Load settings
                LoadSettings();
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
                File.WriteAllText(_configFilePath, jsonString);
                
                OnSettingsChanged();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    var loadedConfig = JsonSerializer.Deserialize<CrosshairConfig>(jsonString);
                    
                    if (loadedConfig != null)
                    {
                        // Copy properties from loaded config
                        CrosshairColorSerialized = loadedConfig.CrosshairColorSerialized;
                        CrosshairSize = Math.Max(10, Math.Min(100, loadedConfig.CrosshairSize));
                        CrosshairThickness = Math.Max(1, Math.Min(10, loadedConfig.CrosshairThickness));
                        CrosshairStyle = loadedConfig.CrosshairStyle ?? "Cross";
                        Visible = loadedConfig.Visible;
                        HotkeyKey = loadedConfig.HotkeyKey;
                        HotkeyUseAlt = loadedConfig.HotkeyUseAlt;
                        HotkeyUseControl = loadedConfig.HotkeyUseControl;
                        HotkeyUseShift = loadedConfig.HotkeyUseShift;
                        HotkeyUseWin = loadedConfig.HotkeyUseWin;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Don't show message box here as it might be called during initialization
            }
        }

        public void RegisterHotkey(Form ownerForm)
        {
            if (_disposed || ownerForm == null || ownerForm.IsDisposed) return;
            
            _ownerForm = ownerForm;
            
            if (_hotkeyRegistered)
            {
                UnregisterHotkey();
            }
                
            try
            {
                int modifiers = 0;
                if (HotkeyUseAlt) modifiers |= MOD_ALT;
                if (HotkeyUseControl) modifiers |= MOD_CONTROL;
                if (HotkeyUseShift) modifiers |= MOD_SHIFT;
                if (HotkeyUseWin) modifiers |= MOD_WIN;
                
                if (RegisterHotKey(ownerForm.Handle, HOTKEY_ID, modifiers, (int)HotkeyKey))
                {
                    _hotkeyRegistered = true;
                }
                else
                {
                    Debug.WriteLine("Failed to register hotkey. It might be in use by another application.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering hotkey: {ex.Message}");
            }
        }

        public void UnregisterHotkey()
        {
            try
            {
                if (_hotkeyRegistered && _ownerForm != null && !_ownerForm.IsDisposed)
                {
                    UnregisterHotKey(_ownerForm.Handle, HOTKEY_ID);
                    _hotkeyRegistered = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering hotkey: {ex.Message}");
            }
        }

        public bool ProcessHotkey(Message m)
        {
            if (_disposed) return false;

            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                Visible = !Visible;
                OnSettingsChanged();
                return true;
            }
            return false;
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
                        UnregisterHotkey();
                        SaveSettings(); // Save settings before disposing
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing CrosshairConfig: {ex.Message}");
                    }
                    _ownerForm = null;
                }
                _disposed = true;
            }
        }
    }
}

