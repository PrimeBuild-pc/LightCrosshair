using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightCrosshair
{
    public sealed class ProfileService : IProfileService
    {
        private static readonly Lazy<ProfileService> _lazy = new(() => new ProfileService());
        public static ProfileService Instance => _lazy.Value;

        private readonly List<CrosshairProfile> _profiles = new();
        private readonly Debouncer _saveDebounce = new(300);
        public IReadOnlyList<CrosshairProfile> Profiles => _profiles;
        public CrosshairProfile Current { get; private set; } = new CrosshairProfile { Name = "Default" };

        public event EventHandler<CrosshairProfile>? CurrentChanged;
        public event EventHandler<ProfilesPersistedEventArgs>? Persisted;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NONE = 0x0000;
        private readonly Dictionary<int, CrosshairProfile> _hotkeyMap = new();
        private int _nextHotkeyId = 1;
        private IntPtr _windowHandle = IntPtr.Zero;

        private ProfileService() { }

        public async Task InitializeAsync()
        {
            var loaded = await ProfileStore.LoadAsync();
            if (loaded.Count == 0)
            {
                loaded.Add(new CrosshairProfile { Name = "Default", EnumShape = CrosshairShape.Cross, Shape = "Cross" });
                await ProfileStore.SaveAtomicAsync(loaded);
            }
            _profiles.Clear();
            _profiles.AddRange(loaded);
            Current = _profiles[0];
            CurrentChanged?.Invoke(this, Current);
            ScheduleSave(true);
            RebuildHotkeys();
        }

        public async Task PersistAsync() => await PersistInternalAsync();

        private async Task PersistInternalAsync()
        {
            bool success = true;
            try { await ProfileStore.SaveAtomicAsync(_profiles); }
            catch { success = false; }
            Persisted?.Invoke(this, new ProfilesPersistedEventArgs(success, DateTime.Now));
        }

        private void ScheduleSave(bool immediate = false)
        {
            if (immediate) { _ = Task.Run(PersistInternalAsync); return; }
            _saveDebounce.Trigger(() => _ = Task.Run(PersistInternalAsync));
        }

        public void Switch(string idOrName)
        {
            var p = _profiles.FirstOrDefault(x => x.Id == idOrName) ?? _profiles.FirstOrDefault(x => x.Name == idOrName);
            if (p != null && !ReferenceEquals(p, Current))
            {
                Current = p;
                CurrentChanged?.Invoke(this, Current);
                ScheduleSave();
            }
        }

        public CrosshairProfile AddClone(CrosshairProfile src, string newName)
        {
            var clone = src.Clone();
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = newName;
            _profiles.Add(clone);
            RebuildHotkeys();
            ScheduleSave();
            return clone;
        }

        public bool Delete(string id)
        {
            if (_profiles.Count <= 1) return false;
            var idx = _profiles.FindIndex(p => p.Id == id);
            if (idx < 0) return false;
            var wasCurrent = ReferenceEquals(_profiles[idx], Current);
            _profiles.RemoveAt(idx);
            if (wasCurrent) Current = _profiles[0];
            if (wasCurrent) CurrentChanged?.Invoke(this, Current);
            ScheduleSave();
            RebuildHotkeys();
            return true;
        }

        public void Update(CrosshairProfile updated)
        {
            var idx = _profiles.FindIndex(p => p.Id == updated.Id);
            if (idx >= 0)
            {
                _profiles[idx] = updated;
                if (ReferenceEquals(Current, _profiles[idx]))
                    CurrentChanged?.Invoke(this, _profiles[idx]);
                RebuildHotkeys();
                ScheduleSave();
            }
        }

        public bool Move(string id, int delta)
        {
            var idx = _profiles.FindIndex(p => p.Id == id);
            if (idx < 0) return false;
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= _profiles.Count) return false;
            var item = _profiles[idx];
            _profiles.RemoveAt(idx);
            _profiles.Insert(newIdx, item);
            CurrentChanged?.Invoke(this, Current);
            ScheduleSave();
            return true;
        }

        public void RegisterHotkeys(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            RebuildHotkeys();
        }

        public void UnregisterHotkeys()
        {
            if (_hotkeyMap.Count == 0) return;
            foreach (var id in _hotkeyMap.Keys.ToList())
            { try { UnregisterHotKey(_windowHandle, id); } catch { } }
            _hotkeyMap.Clear();
            _nextHotkeyId = 1;
        }

        public bool ProcessHotkeyMessage(Message m)
        {
            if (m.Msg != WM_HOTKEY) return false;
            int id = m.WParam.ToInt32();
            if (_hotkeyMap.TryGetValue(id, out var profile)) { Switch(profile.Id); return true; }
            return false;
        }

        private void RebuildHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            foreach (var id in _hotkeyMap.Keys.ToList())
            { try { UnregisterHotKey(_windowHandle, id); } catch { } }
            _hotkeyMap.Clear();
            _nextHotkeyId = 1;
            foreach (var p in _profiles)
            {
                if (p.HotKey == Keys.None) continue;
                try
                {
                    int id = _nextHotkeyId++;
                    if (RegisterHotKey(_windowHandle, id, MOD_NONE, (uint)p.HotKey)) _hotkeyMap[id] = p;
                }
                catch { }
            }
        }
    }
}
