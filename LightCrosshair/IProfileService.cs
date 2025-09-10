using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightCrosshair
{
    /// <summary>Unified profile access + persistence + hotkey dispatch.</summary>
    public interface IProfileService
    {
        IReadOnlyList<CrosshairProfile> Profiles { get; }
        CrosshairProfile Current { get; }
        event EventHandler<CrosshairProfile>? CurrentChanged;
        event EventHandler<ProfilesPersistedEventArgs>? Persisted;

        Task InitializeAsync();
        Task PersistAsync();

        void Switch(string idOrName);
        CrosshairProfile AddClone(CrosshairProfile src, string newName);
        bool Delete(string id);
        void Update(CrosshairProfile updated);
        bool Move(string id, int delta);

        void RegisterHotkeys(IntPtr windowHandle);
        void UnregisterHotkeys();
        bool ProcessHotkeyMessage(Message m);
    }

    public sealed class ProfilesPersistedEventArgs : EventArgs
    {
        public bool Success { get; }
        public DateTime Timestamp { get; }
        public ProfilesPersistedEventArgs(bool success, DateTime ts)
        { Success = success; Timestamp = ts; }
    }
}
