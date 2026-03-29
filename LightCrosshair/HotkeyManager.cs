using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LightCrosshair
{
    /// <summary>
    /// Manages global hotkey registration using Win32 RegisterHotKey/UnregisterHotKey.
    /// Prevents duplicate registrations and provides a thread-safe Singleton interface.
    /// </summary>
    public class HotkeyManager
    {
        private static readonly Lazy<HotkeyManager> _instance = 
            new Lazy<HotkeyManager>(() => new HotkeyManager());

        public static HotkeyManager Instance => _instance.Value;

        private IntPtr _windowHandle = IntPtr.Zero;
        private readonly Dictionary<int, (int ModifierKeys, int VirtualKey)> _registeredHotkeys = 
            new Dictionary<int, (int, int)>();
        private readonly object _lockObject = new object();

        // Specific IDs we want to preserve to avoid breaking existing int references
        public const int TOGGLE_VISIBILITY_ID = 9000;
        public const int CYCLE_PROFILE_NEXT_ID = 9001;
        public const int CYCLE_PROFILE_PREV_ID = 9002;
        public const int TOGGLE_SETTINGS_WINDOW_ID = 9003;

        private int _nextHotkeyId = 1;

        // Modifier key flags
        public const int MOD_ALT = 1;
        public const int MOD_CONTROL = 2;
        public const int MOD_SHIFT = 4;
        public const int MOD_WIN = 8;
        public const int MOD_NOREPEAT = 0x4000;

        private HotkeyManager()
        {
        }

        public void SetWindowHandle(IntPtr windowHandle)
        {
            lock (_lockObject)
            {
                _windowHandle = windowHandle;
            }
        }

        public bool RegisterHotkeyWithId(int id, int modifierKeys, int virtualKey)
        {
            lock (_lockObject)
            {
                if (_windowHandle == IntPtr.Zero) return false;

                // Check for collision
                foreach (var existing in _registeredHotkeys)
                {
                    if (existing.Value.ModifierKeys == modifierKeys && 
                        existing.Value.VirtualKey == virtualKey)
                    {
                        return false; // Collision detected
                    }
                }

                if (!RegisterHotKey(_windowHandle, id, modifierKeys | MOD_NOREPEAT, virtualKey))
                {
                    return false;
                }

                _registeredHotkeys[id] = (modifierKeys, virtualKey);
                return true;
            }
        }

        public bool RegisterHotkey(int modifierKeys, int virtualKey, out int hotkeyId)
        {
            hotkeyId = 0;
            lock (_lockObject)
            {
                // reserve fixed IDs used by global hotkeys
                while (_nextHotkeyId >= 9000 && _nextHotkeyId <= 9003) _nextHotkeyId++;
                int idToTry = _nextHotkeyId++;
                if (RegisterHotkeyWithId(idToTry, modifierKeys, virtualKey))
                {
                    hotkeyId = idToTry;
                    return true;
                }
                _nextHotkeyId--;
                return false;
            }
        }

        public bool UnregisterHotkey(int hotkeyId)
        {
            lock (_lockObject)
            {
                if (!_registeredHotkeys.ContainsKey(hotkeyId))
                    return false;

                if (_windowHandle != IntPtr.Zero)
                    UnregisterHotKey(_windowHandle, hotkeyId);

                _registeredHotkeys.Remove(hotkeyId);
                return true;
            }
        }

        public void UnregisterAll()
        {
            lock (_lockObject)
            {
                var hotkeyIds = new List<int>(_registeredHotkeys.Keys);
                foreach (var id in hotkeyIds)
                {
                    UnregisterHotKey(_windowHandle, id);
                    _registeredHotkeys.Remove(id);
                }
                _nextHotkeyId = 1;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}