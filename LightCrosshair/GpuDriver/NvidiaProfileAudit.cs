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
        InvalidTarget,
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
            FromStatus(NvidiaProfileAuditStatus.InvalidTarget, string.Empty, null, statusText);

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

        public const uint LowLatencyModeSettingId = 0x10835000u;
        public const uint LowLatencyCplStateSettingId = 0x0005F543u;
        public const uint VerticalSyncSettingId = 0x00A879CFu;
        public const uint GSyncApplicationModeSettingId = 0x1194F158u;
        public const uint GSyncApplicationStateSettingId = 0x10A879CFu;
        public const uint GSyncApplicationRequestedStateSettingId = 0x10A879ACu;

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
                    LowLatencyModeSettingId,
                    "Low Latency Mode Enabled",
                    NvidiaProfileSettingUiHint.Toggle,
                    IsReadOnly: false,
                    IsReferenceOnly: false,
                    "Per-application low latency mode flag. LightCrosshair only exposes Off and On.",
                    new Dictionary<uint, string>
                    {
                        [0u] = "Off",
                        [1u] = "On"
                    }),
                new NvidiaProfileSettingDefinition(
                    LowLatencyCplStateSettingId,
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
                    VerticalSyncSettingId,
                    "Vertical Sync",
                    NvidiaProfileSettingUiHint.Dropdown,
                    IsReadOnly: false,
                    IsReferenceOnly: false,
                    "Per-application vertical sync profile value. LightCrosshair only exposes known safe values.",
                    new Dictionary<uint, string>
                    {
                        [0x60925292u] = "Application controlled",
                        [0x08416747u] = "Force off",
                        [0x47814940u] = "Force on",
                        [0x18888888u] = "Fast Sync"
                    }),
                new NvidiaProfileSettingDefinition(
                    GSyncApplicationModeSettingId,
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
                    GSyncApplicationStateSettingId,
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
                    GSyncApplicationRequestedStateSettingId,
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

    public enum NvidiaProfileWriteStatus
    {
        Success,
        Unsupported,
        InvalidTarget,
        NotAllowed,
        NoProfile,
        Conflict,
        RestoreUnavailable,
        Error
    }

    public sealed record NvidiaProfileSettingValueOption(uint RawValue, string DisplayName);

    public sealed record NvidiaProfileSettingWriteDefinition(
        uint SettingId,
        string DisplayName,
        IReadOnlyList<NvidiaProfileSettingValueOption> AllowedValues)
    {
        public bool AllowsValue(uint rawValue) => AllowedValues.Any(value => value.RawValue == rawValue);

        public string FormatFriendlyValue(uint rawValue) =>
            AllowedValues.FirstOrDefault(value => value.RawValue == rawValue)?.DisplayName ?? $"0x{rawValue:X8}";
    }

    public sealed record NvidiaProfileSettingWriteRequest(uint SettingId, uint RawValue);

    public sealed record NvidiaProfileSettingBackup(
        string TargetApplicationPath,
        string? ProfileName,
        uint SettingId,
        bool WasPresent,
        uint? PreviousRawValue,
        uint WrittenRawValue,
        DateTimeOffset Timestamp,
        string LightCrosshairVersion);

    public sealed record NvidiaProfileSettingWriteResult(
        NvidiaProfileWriteStatus Status,
        string StatusText,
        string TargetApplicationPath,
        string? ProfileName,
        NvidiaProfileSettingBackup? Backup = null)
    {
        public bool Succeeded => Status == NvidiaProfileWriteStatus.Success;

        public static NvidiaProfileSettingWriteResult FromStatus(
            NvidiaProfileWriteStatus status,
            string statusText,
            string targetApplicationPath = "",
            string? profileName = null,
            NvidiaProfileSettingBackup? backup = null) =>
            new(status, statusText, targetApplicationPath, profileName, backup);
    }

    public sealed record NvidiaProfileSettingWriteValidation(
        NvidiaProfileWriteStatus Status,
        string StatusText,
        NvidiaProfileSettingWriteDefinition? Definition)
    {
        public bool IsValid => Status == NvidiaProfileWriteStatus.Success;
    }

    public static class NvidiaProfileSettingWriteCatalog
    {
        private static readonly ReadOnlyCollection<NvidiaProfileSettingWriteDefinition> Definitions =
            new[]
            {
                new NvidiaProfileSettingWriteDefinition(
                    NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
                    "Low Latency Mode",
                    new[]
                    {
                        new NvidiaProfileSettingValueOption(0u, "Off"),
                        new NvidiaProfileSettingValueOption(1u, "On")
                    }),
                new NvidiaProfileSettingWriteDefinition(
                    NvidiaProfileSettingCatalog.VerticalSyncSettingId,
                    "Vertical Sync",
                    new[]
                    {
                        new NvidiaProfileSettingValueOption(0x60925292u, "Application controlled"),
                        new NvidiaProfileSettingValueOption(0x08416747u, "Force off"),
                        new NvidiaProfileSettingValueOption(0x47814940u, "Force on"),
                        new NvidiaProfileSettingValueOption(0x18888888u, "Fast Sync")
                    })
            }.AsReadOnly();

        public static IReadOnlyList<NvidiaProfileSettingWriteDefinition> All => Definitions;

        public static bool TryGet(uint settingId, out NvidiaProfileSettingWriteDefinition definition)
        {
            definition = Definitions.FirstOrDefault(item => item.SettingId == settingId)!;
            return definition != null;
        }

        public static bool CanWrite(uint settingId, uint rawValue) =>
            TryGet(settingId, out var definition) && definition.AllowsValue(rawValue);

        public static NvidiaProfileSettingWriteValidation ValidateRequest(
            string? applicationExePath,
            NvidiaProfileSettingWriteRequest request)
        {
            if (string.IsNullOrWhiteSpace(applicationExePath))
            {
                return new NvidiaProfileSettingWriteValidation(
                    NvidiaProfileWriteStatus.InvalidTarget,
                    "Select a target application before applying NVIDIA profile controls.",
                    null);
            }

            if (!TryGet(request.SettingId, out var definition))
            {
                return new NvidiaProfileSettingWriteValidation(
                    NvidiaProfileWriteStatus.NotAllowed,
                    "This NVIDIA profile setting is not writable from LightCrosshair.",
                    null);
            }

            if (!definition.AllowsValue(request.RawValue))
            {
                return new NvidiaProfileSettingWriteValidation(
                    NvidiaProfileWriteStatus.NotAllowed,
                    $"'{definition.DisplayName}' does not allow value 0x{request.RawValue:X8}.",
                    definition);
            }

            return new NvidiaProfileSettingWriteValidation(
                NvidiaProfileWriteStatus.Success,
                string.Empty,
                definition);
        }
    }
}
