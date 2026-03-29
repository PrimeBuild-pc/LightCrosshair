using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightCrosshair
{
    public sealed class ProfileService : IProfileService
    {
        private static readonly Lazy<ProfileService> _lazy = new(() => new ProfileService());
        public static ProfileService Instance => _lazy.Value;
        public const int MaxProfiles = 10;

        private readonly List<CrosshairProfile> _profiles = new();
        private readonly Debouncer _saveDebounce = new(300);
        private readonly SemaphoreSlim _persistSemaphore = new(1, 1);
        private int _pendingPersistRequests;
        public IReadOnlyList<CrosshairProfile> Profiles => _profiles;
        public CrosshairProfile Current { get; private set; } = new CrosshairProfile { Name = "Default" };

        public event EventHandler<CrosshairProfile>? CurrentChanged;
        public event EventHandler<ProfilesPersistedEventArgs>? Persisted;

        private const int WM_HOTKEY = 0x0312;
        private readonly Dictionary<int, CrosshairProfile> _hotkeyMap = new();
        
        private IntPtr _windowHandle = IntPtr.Zero;

        private ProfileService() { }

        public async Task InitializeAsync()
        {
            var loaded = await ProfileStore.LoadAsync();

            var prefs = PreferencesStore.Load();
            if (loaded.Count == 0)
            {
                // First launch default: simple red cross
                var def = new CrosshairProfile
                {
                    Name = "Default",
                    EnumShape = CrosshairShape.Cross,
                    Shape = "Cross",
                    OuterColor = System.Drawing.Color.Red,
                    EdgeColor = System.Drawing.Color.Red,
                    InnerColor = System.Drawing.Color.Transparent,
                    Thickness = 3,
                    EdgeThickness = 0,
                    GapSize = 0,
                    InnerGapSize = 0,
                };
                loaded.Add(def);
                await ProfileStore.SaveAtomicAsync(loaded);
                prefs.FirstRunDone = true;
                PreferencesStore.Save(prefs);
            }
            _profiles.Clear();
            _profiles.AddRange(loaded);
            var restoredCurrent = !string.IsNullOrWhiteSpace(prefs.LastProfileId)
                ? _profiles.FirstOrDefault(p => p.Id == prefs.LastProfileId)
                : null;
            Current = restoredCurrent ?? _profiles[0];
            PersistLastProfileId(Current.Id);
            // If first profile starts as CircleDot/CrossDot, normalize initial parameters
            try
            {
                if (string.Equals(Current.Shape, "CircleDot", StringComparison.OrdinalIgnoreCase))
                {
                    var d = CompositeDefaults.GetCompositeDefaults(CompositeShapeType.CircleDot);
                    if (d != null)
                    {
                        Current.Size = d.OuterSize; Current.Thickness = d.OuterThickness; Current.GapSize = d.OuterGapSize;
                        Current.InnerSize = d.InnerSize; Current.InnerThickness = d.InnerThickness; Current.InnerGapSize = d.InnerGapSize;
                    }
                }
                else if (string.Equals(Current.Shape, "CrossDot", StringComparison.OrdinalIgnoreCase))
                {
                    var d = CompositeDefaults.GetCompositeDefaults(CompositeShapeType.CrossDot);
                    if (d != null)
                    {
                        Current.Size = d.OuterSize; Current.Thickness = d.OuterThickness; Current.GapSize = d.OuterGapSize;
                        Current.InnerSize = d.InnerSize; Current.InnerThickness = d.InnerThickness; Current.InnerGapSize = d.InnerGapSize;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "ProfileService.InitializeAsync: defaults migration");
            }
            CurrentChanged?.Invoke(this, Current);
            ScheduleSave(true);
            RebuildHotkeys();
        }

        public async Task PersistAsync() => await PersistInternalAsync();

        private async Task PersistInternalAsync()
        {
            Interlocked.Increment(ref _pendingPersistRequests);

            await _persistSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                while (Interlocked.Exchange(ref _pendingPersistRequests, 0) > 0)
                {
                    bool success = true;
                    try
                    {
                        var snapshot = _profiles.Select(p => p.Clone()).ToList();
                        await ProfileStore.SaveAtomicAsync(snapshot).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        Program.LogError(ex, "ProfileService.PersistInternalAsync");
                    }

                    Persisted?.Invoke(this, new ProfilesPersistedEventArgs(success, DateTime.Now));
                }
            }
            finally
            {
                _persistSemaphore.Release();
            }
        }

        private void ScheduleSave(bool immediate = false)
        {
            if (immediate)
            {
                _ = PersistInternalAsync();
                return;
            }

            _saveDebounce.Trigger(() => _ = PersistInternalAsync());
        }

        public void Switch(string idOrName)
        {
            try
            {
                Program.LogDebug($"Switch request -> {idOrName}", nameof(ProfileService));
                var p = _profiles.FirstOrDefault(x => x.Id == idOrName) ?? _profiles.FirstOrDefault(x => x.Name == idOrName);
                if (p != null && !ReferenceEquals(p, Current))
                {
                    Current = p;
                    PersistLastProfileId(Current.Id);
                    CurrentChanged?.Invoke(this, Current);
                    ScheduleSave();
                }
            }
            catch (Exception ex) { Program.LogError(ex, "ProfileService.Switch"); }
        }

        public CrosshairProfile AddClone(CrosshairProfile src, string newName)
        {
            if (_profiles.Count >= MaxProfiles)
            {
                throw new InvalidOperationException($"Maximum number of profiles reached ({MaxProfiles}).");
            }

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
            if (wasCurrent)
            {
                PersistLastProfileId(Current.Id);
                CurrentChanged?.Invoke(this, Current);
            }
            ScheduleSave();
            RebuildHotkeys();
            return true;
        }

        public void Update(CrosshairProfile updated)
        {
            try
            {
                Program.LogDebug($"Update profile -> {updated.Name} ({updated.Id})", nameof(ProfileService));
                var idx = _profiles.FindIndex(p => p.Id == updated.Id);
                if (idx >= 0)
                {
                    var existing = _profiles[idx];
                    bool wasCurrent = existing.Id == Current.Id;
                    bool sameInstance = ReferenceEquals(existing, updated);
                    // Avoid no-op updates that can trigger event loops
                    if (wasCurrent && !sameInstance && updated.ContentEquals(Current))
                    {
                        Program.LogDebug("Update skipped (no changes)", nameof(ProfileService));
                        return;
                    }
                    _profiles[idx] = updated;
                    if (wasCurrent)
                    {
                        Current = updated;
                        PersistLastProfileId(Current.Id);
                        CurrentChanged?.Invoke(this, Current);
                    }
                    RebuildHotkeys();
                    ScheduleSave();
                }
            }
            catch (Exception ex) { Program.LogError(ex, "ProfileService.Update"); }
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
            { try { HotkeyManager.Instance.UnregisterHotkey(id); } catch (Exception ex) { Program.LogError(ex, "ProfileService: UnregisterHotKey (Dispose)"); } }
            _hotkeyMap.Clear();
            
        }

        public bool ProcessHotkeyMessage(Message m)
        {
            try
            {
                if (m.Msg != WM_HOTKEY) return false;
                int id = m.WParam.ToInt32();
                if (_hotkeyMap.TryGetValue(id, out var profile)) { Program.LogDebug($"Hotkey -> {profile.Name}", nameof(ProfileService)); Switch(profile.Id); return true; }
                return false;
            }
            catch (Exception ex) { Program.LogError(ex, "ProfileService.ProcessHotkeyMessage"); return false; }
        }

        private void RebuildHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            foreach (var id in _hotkeyMap.Keys.ToList())
            { try { HotkeyManager.Instance.UnregisterHotkey(id); } catch (Exception ex) { Program.LogError(ex, "ProfileService: UnregisterHotKey (Re-register)"); } }   
            _hotkeyMap.Clear();
            
            foreach (var p in _profiles)
            {
                if (p.HotKey == Keys.None) continue;
                try
                {
                    if (HotkeyManager.Instance.RegisterHotkey(0, (int)p.HotKey, out int id)) _hotkeyMap[id] = p;
                }
                catch (Exception ex) { Program.LogError(ex, "ProfileService: RegisterHotKey"); }
            }
        }

        private static void PersistLastProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            try
            {
                var prefs = PreferencesStore.Load();
                if (string.Equals(prefs.LastProfileId, id, StringComparison.Ordinal))
                {
                    return;
                }

                prefs.LastProfileId = id;
                PreferencesStore.Save(prefs);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "ProfileService.PersistLastProfileId");
            }
        }
    }
}
