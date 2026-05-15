#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NvAPIWrapper.DRS;

namespace LightCrosshair.GpuDriver
{
    public enum NvidiaProfileAuditStatus
    {
        Present,
        NotPresent,
        NoProfile,
        Unsupported,
        Error
    }

    public enum NvidiaProfileSettingUiHint
    {
        ReadOnlyBadge,
        Toggle,
        Dropdown,
        Number,
        RawEditor
    }

    public sealed record NvidiaProfileSettingDefinition(
        uint SettingId,
        string DisplayName,
        NvidiaProfileSettingUiHint UiHint,
        bool IsReadOnly,
        bool IsReferenceOnly,
        string HelpText,
        IReadOnlyDictionary<uint, string> KnownValues)
    {
        public string FormatFriendlyValue(uint rawValue) =>
            KnownValues.TryGetValue(rawValue, out string? friendlyValue)
                ? friendlyValue
                : $"0x{rawValue:X8}";
    }

    public sealed record NvidiaProfileSettingAuditItem(
        NvidiaProfileSettingDefinition Definition,
        NvidiaProfileAuditStatus Status,
        uint? RawValue,
        string FriendlyValue,
        string StatusText)
    {
        public uint SettingId => Definition.SettingId;
        public string DisplayName => Definition.DisplayName;
        public bool IsReadOnly => Definition.IsReadOnly;
        public NvidiaProfileSettingUiHint UiHint => Definition.UiHint;
        public string HelpText => Definition.HelpText;
    }

    public sealed record NvidiaProfileAuditResult(
        NvidiaProfileAuditStatus Status,
        string TargetApplicationPath,
        string? ProfileName,
        IReadOnlyList<NvidiaProfileSettingAuditItem> Settings,
        string StatusText)
    {
        public static NvidiaProfileAuditResult Unsupported(string statusText) =>
            FromStatus(NvidiaProfileAuditStatus.Unsupported, string.Empty, null, statusText);

        public static NvidiaProfileAuditResult InvalidTarget(string statusText) =>
            FromStatus(NvidiaProfileAuditStatus.Error, string.Empty, null, statusText);

        public static NvidiaProfileAuditResult FromStatus(
            NvidiaProfileAuditStatus status,
            string targetApplicationPath,
            string? profileName,
            string statusText)
        {
            var settings = NvidiaProfileSettingCatalog.All
                .Select(definition => new NvidiaProfileSettingAuditItem(
                    definition,
                    status,
                    null,
                    string.Empty,
                    statusText))
                .ToArray();

            return new NvidiaProfileAuditResult(status, targetApplicationPath, profileName, settings, statusText);
        }
    }

    public static class NvidiaProfileSettingCatalog
    {
        public static readonly uint DriverFpsCapSettingId =
            (uint)KnownSettingId.PerformanceStateFrameRateLimiter;

        private static readonly ReadOnlyCollection<NvidiaProfileSettingDefinition> Definitions =
            new[]
            {
                new NvidiaProfileSettingDefinition(
                    DriverFpsCapSettingId,
                    "Driver FPS Cap",
                    NvidiaProfileSettingUiHint.Number,
                    IsReadOnly: true,
                    IsReferenceOnly: false,
                    "Reads the per-application NVIDIA driver FPS cap. Existing apply/clear controls handle writes separately.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Off"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x10835000u,
                    "Low Latency Mode Enabled",
                    NvidiaProfileSettingUiHint.Toggle,
                    IsReadOnly: true,
                    IsReferenceOnly: false,
                    "Read-only audit of the driver low-latency enabled flag. LightCrosshair does not write this setting yet.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Off",
                        [1u] = "On"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x0005F543u,
                    "Low Latency CPL State",
                    NvidiaProfileSettingUiHint.ReadOnlyBadge,
                    IsReadOnly: true,
                    IsReferenceOnly: true,
                    "Reference-only Control Panel state related to low latency mode; it is not treated as a writable LightCrosshair setting.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Off",
                        [1u] = "On",
                        [2u] = "Ultra"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x00A879CFu,
                    "Vertical Sync",
                    NvidiaProfileSettingUiHint.Dropdown,
                    IsReadOnly: true,
                    IsReferenceOnly: false,
                    "Read-only audit of the per-application NVIDIA vertical sync profile value.",
                    new Dictionary<uint, string>
                    {
                        [0x60925292u] = "Application controlled",
                        [0x08416747u] = "Force off",
                        [0x47814940u] = "Force on",
                        [0x18888888u] = "Fast Sync"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x1194F158u,
                    "G-SYNC Application Mode",
                    NvidiaProfileSettingUiHint.Dropdown,
                    IsReadOnly: true,
                    IsReferenceOnly: true,
                    "Read-only audit of the per-application G-SYNC mode; display and driver state can affect behavior.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Off",
                        [1u] = "Fullscreen only",
                        [2u] = "Fullscreen and Windowed"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x10A879CFu,
                    "G-SYNC Application State",
                    NvidiaProfileSettingUiHint.ReadOnlyBadge,
                    IsReadOnly: true,
                    IsReferenceOnly: true,
                    "Reference-only G-SYNC application state until apply behavior is validated.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Allow",
                        [1u] = "Force Off",
                        [2u] = "Disallow"
                    }),
                new NvidiaProfileSettingDefinition(
                    0x10A879ACu,
                    "G-SYNC Application Requested State",
                    NvidiaProfileSettingUiHint.ReadOnlyBadge,
                    IsReadOnly: true,
                    IsReferenceOnly: true,
                    "Reference-only requested G-SYNC application state until apply behavior is validated.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Allow",
                        [1u] = "Force Off",
                        [2u] = "Disallow"
                    })
            }.AsReadOnly();

        public static IReadOnlyList<NvidiaProfileSettingDefinition> All => Definitions;
    }
}
