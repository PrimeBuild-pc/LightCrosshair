using System;
using System.Collections.Generic;
using System.Drawing;
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
        internal const string DefaultProfileId = "default-immutable";

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
            EnsureImmutableDefaultProfile(loaded);

            _profiles.Clear();
            _profiles.AddRange(loaded);

            var prefs = PreferencesStore.Load();
            var persistedState = await CurrentStateStore.LoadAsync();
            if (persistedState?.CurrentProfile != null)
            {
                Current = NormalizeWorkingState(persistedState.CurrentProfile);
            }
            else
            {
                var startupTemplate = ResolveStartupTemplate(prefs.LastProfileId);
                Current = CreateWorkingCopy(startupTemplate);
                if (startupTemplate.IsImmutableDefault)
                {
                    ApplyImmutableDefaultRuntimeConfig();
                    prefs.FirstRunDone = true;
                    PreferencesStore.Save(prefs);
                }
            }

            PersistLastProfileId(ResolveCurrentSourceProfileId(prefs.LastProfileId));
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

        private CrosshairProfile ResolveStartupTemplate(string? lastProfileId)
        {
            if (!string.IsNullOrWhiteSpace(lastProfileId))
            {
                var byId = _profiles.FirstOrDefault(p => string.Equals(p.Id, lastProfileId, StringComparison.OrdinalIgnoreCase));
                if (byId != null)
                {
                    return byId;
                }
            }

            return _profiles.FirstOrDefault(p => p.IsImmutableDefault)
                ?? _profiles.FirstOrDefault()
                ?? BuildImmutableDefaultProfile();
        }

        private static string NewWorkingProfileId() => $"working::{Guid.NewGuid():N}";

        private static CrosshairProfile CreateWorkingCopy(CrosshairProfile template)
        {
            var working = template.Clone();
            working.Id = NewWorkingProfileId();
            working.SourceProfileId = string.IsNullOrWhiteSpace(template.Id) ? DefaultProfileId : template.Id;
            return working;
        }

        private static CrosshairProfile NormalizeWorkingState(CrosshairProfile profile)
        {
            var working = profile.Clone();
            if (string.IsNullOrWhiteSpace(working.SourceProfileId))
            {
                working.SourceProfileId = DefaultProfileId;
            }

            if (string.IsNullOrWhiteSpace(working.Id) || !working.Id.StartsWith("working::", StringComparison.Ordinal))
            {
                working.Id = NewWorkingProfileId();
            }

            return working;
        }

        private string ResolveCurrentSourceProfileId(string fallback)
        {
            if (!string.IsNullOrWhiteSpace(Current.SourceProfileId)
                && _profiles.Any(p => string.Equals(p.Id, Current.SourceProfileId, StringComparison.OrdinalIgnoreCase)))
            {
                return Current.SourceProfileId;
            }

            if (!string.IsNullOrWhiteSpace(fallback)
                && _profiles.Any(p => string.Equals(p.Id, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                Current.SourceProfileId = fallback;
                return fallback;
            }

            var defaultProfile = _profiles.FirstOrDefault(p => p.IsImmutableDefault);
            if (defaultProfile != null)
            {
                Current.SourceProfileId = defaultProfile.Id;
                return defaultProfile.Id;
            }

            return DefaultProfileId;
        }

        private static void EnsureImmutableDefaultProfile(List<CrosshairProfile> profiles)
        {
            var canonical = BuildImmutableDefaultProfile();

            profiles.RemoveAll(p =>
                p.IsImmutableDefault
                || string.Equals(p.Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "Default", StringComparison.OrdinalIgnoreCase)
                || p.Id.StartsWith("working::", StringComparison.OrdinalIgnoreCase));

            profiles.Insert(0, canonical);
        }

        private static CrosshairProfile BuildImmutableDefaultProfile()
        {
            return new CrosshairProfile
            {
                Id = DefaultProfileId,
                SourceProfileId = DefaultProfileId,
                Name = "Default",
                IsImmutableDefault = true,
                EnableCustomCrosshair = true,
                Shape = "Cross",
                EnumShape = CrosshairShape.Cross,
                OuterColor = Color.Red,
                EdgeColor = Color.Red,
                InnerColor = Color.Transparent,
                FillColor = Color.Transparent,
                Size = 12,
                Thickness = 2,
                EdgeThickness = 0,
                GapSize = 0,
                InnerGapSize = 0,
                HasDisplayColorProfile = true,
                DisplayEnableGammaOverride = false,
                DisplayGammaValue = 100,
                DisplayContrastValue = 100,
                DisplayBrightnessValue = 100,
                DisplayVibranceValue = 50,
                DisplayTargetProcessName = string.Empty,
                SchemaVersion = ProfileSchema.Current,
            };
        }

        private static void ApplyImmutableDefaultRuntimeConfig()
        {
            var cfg = CrosshairConfig.Instance;
            cfg.Visible = true;
            cfg.EnableFpsOverlay = true;
            cfg.FpsOverlayColorSerialized = "0,0,0";
            cfg.FpsOverlayBgColorSerialized = "255,0,255";
            cfg.ShowGenFrames = false;
            cfg.EnableGammaOverride = false;
            cfg.TargetProcessName = string.Empty;
            cfg.GammaValue = 100;
            cfg.ContrastValue = 100;
            cfg.BrightnessValue = 100;
            cfg.VibranceValue = 50;
            cfg.SaveSettings();
            DisplayManager.CheckForegroundAndApply(forceUpdate: true);
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
                        await CurrentStateStore.SaveAtomicAsync(new CurrentStateSnapshot
                        {
                            CurrentProfile = Current.Clone()
                        }).ConfigureAwait(false);
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
                if (p != null)
                {
                    Current = CreateWorkingCopy(p);
                    if (p.IsImmutableDefault)
                    {
                        ApplyImmutableDefaultRuntimeConfig();
                    }

                    PersistLastProfileId(p.Id);
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
            clone.SourceProfileId = clone.Id;
            clone.IsImmutableDefault = false;
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
            if (_profiles[idx].IsImmutableDefault) return false;
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
                    if (existing.IsImmutableDefault)
                    {
                        Program.LogDebug("Update skipped on immutable default profile", nameof(ProfileService));
                        return;
                    }

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
                else if (string.Equals(Current.Id, updated.Id, StringComparison.Ordinal))
                {
                    if (updated.ContentEquals(Current))
                    {
                        Program.LogDebug("Working-state update skipped (no changes)", nameof(ProfileService));
                        return;
                    }

                    Current = updated;
                    if (string.IsNullOrWhiteSpace(Current.SourceProfileId))
                    {
                        Current.SourceProfileId = ResolveCurrentSourceProfileId(DefaultProfileId);
                    }

                    CurrentChanged?.Invoke(this, Current);
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
