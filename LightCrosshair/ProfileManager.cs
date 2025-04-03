using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightCrosshair
{
    public class ProfileManager
    {
        // Windows API for hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Constants for hotkey registration
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        // List of profiles
        private List<CrosshairProfile> _profiles;
        
        // Current profile
        private CrosshairProfile _currentProfile;
        
        // Form handle for hotkey registration
        private IntPtr _formHandle;
        
        // Dictionary to map hotkey IDs to profiles
        private Dictionary<int, CrosshairProfile> _hotkeyMap;
        
        // Next available hotkey ID
        private int _nextHotkeyId = 1;

        // Event for profile changes
        public event EventHandler<CrosshairProfile> ProfileChanged;

        public ProfileManager(IntPtr formHandle)
        {
            _formHandle = formHandle;
            _hotkeyMap = new Dictionary<int, CrosshairProfile>();
            _profiles = CrosshairProfile.LoadProfiles();
            
            if (_profiles.Count > 0)
            {
                _currentProfile = _profiles[0];
            }
            else
            {
                _currentProfile = new CrosshairProfile();
                _profiles.Add(_currentProfile);
                _currentProfile.Save();
            }

            // Register hotkeys for all profiles
            RegisterAllHotkeys();
        }

        public CrosshairProfile CurrentProfile => _currentProfile;

        public List<CrosshairProfile> Profiles => _profiles;

        public void AddProfile(CrosshairProfile profile)
        {
            _profiles.Add(profile);
            profile.Save();
            
            // Register hotkey if set
            if (profile.HotKey != Keys.None)
            {
                RegisterProfileHotkey(profile);
            }
        }

        public void UpdateProfile(CrosshairProfile profile)
        {
            // Find the profile in the list
            int index = _profiles.FindIndex(p => p.Name == profile.Name);
            if (index >= 0)
            {
                // Update the profile
                _profiles[index] = profile;
                profile.Save();
                
                // Update hotkey registration
                UnregisterProfileHotkey(profile);
                if (profile.HotKey != Keys.None)
                {
                    RegisterProfileHotkey(profile);
                }
                
                // If this is the current profile, notify listeners
                if (_currentProfile.Name == profile.Name)
                {
                    _currentProfile = profile;
                    OnProfileChanged();
                }
            }
        }

        public void DeleteProfile(CrosshairProfile profile)
        {
            // Don't delete the last profile
            if (_profiles.Count <= 1)
                return;
                
            // Remove from list
            _profiles.RemoveAll(p => p.Name == profile.Name);
            
            // Delete file
            CrosshairProfile.DeleteProfile(profile.Name);
            
            // Unregister hotkey
            UnregisterProfileHotkey(profile);
            
            // If this was the current profile, switch to another one
            if (_currentProfile.Name == profile.Name)
            {
                _currentProfile = _profiles[0];
                OnProfileChanged();
            }
        }

        public void SwitchToProfile(CrosshairProfile profile)
        {
            _currentProfile = profile;
            OnProfileChanged();
        }

        public void SwitchToProfile(string profileName)
        {
            var profile = _profiles.Find(p => p.Name == profileName);
            if (profile != null)
            {
                _currentProfile = profile;
                OnProfileChanged();
            }
        }

        public bool ProcessHotkey(Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();
                if (_hotkeyMap.TryGetValue(hotkeyId, out CrosshairProfile profile))
                {
                    SwitchToProfile(profile);
                    return true;
                }
            }
            return false;
        }

        private void RegisterAllHotkeys()
        {
            foreach (var profile in _profiles)
            {
                if (profile.HotKey != Keys.None)
                {
                    RegisterProfileHotkey(profile);
                }
            }
        }

        private void RegisterProfileHotkey(CrosshairProfile profile)
        {
            if (profile.HotKey == Keys.None)
                return;

            try
            {
                int id = _nextHotkeyId++;
                uint key = (uint)profile.HotKey;
                
                if (RegisterHotKey(_formHandle, id, MOD_NONE, key))
                {
                    _hotkeyMap[id] = profile;
                }
            }
            catch
            {
                // Silently fail if registration fails
            }
        }

        private void UnregisterProfileHotkey(CrosshairProfile profile)
        {
            // Find the hotkey ID for this profile
            int hotkeyId = -1;
            foreach (var kvp in _hotkeyMap)
            {
                if (kvp.Value.Name == profile.Name)
                {
                    hotkeyId = kvp.Key;
                    break;
                }
            }

            // Unregister if found
            if (hotkeyId != -1)
            {
                UnregisterHotKey(_formHandle, hotkeyId);
                _hotkeyMap.Remove(hotkeyId);
            }
        }

        public void UnregisterAllHotkeys()
        {
            foreach (int id in _hotkeyMap.Keys)
            {
                UnregisterHotKey(_formHandle, id);
            }
            _hotkeyMap.Clear();
        }

        private void OnProfileChanged()
        {
            ProfileChanged?.Invoke(this, _currentProfile);
        }
    }
}
