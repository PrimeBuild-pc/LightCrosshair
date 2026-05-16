#nullable enable

using System;

namespace LightCrosshair.GpuDriver
{
    public static class NvidiaHelperOperations
    {
        public const string AuditProfile = "audit-profile";
        public const string ApplyProfileSetting = "apply-profile-setting";
        public const string RestoreProfileSetting = "restore-profile-setting";
        public const string SetFpsCap = "set-fps-cap";
        public const string ClearFpsCap = "clear-fps-cap";
        public const string SetVibrance = "set-vibrance";
    }

    public sealed record NvidiaHelperRequest
    {
        public string Operation { get; init; } = string.Empty;
        public string? TargetExePath { get; init; }
        public NvidiaProfileSettingWriteRequest? ProfileWriteRequest { get; init; }
        public NvidiaProfileSettingBackup? ProfileBackup { get; init; }
        public uint? SettingId { get; init; }
        public int? TargetFps { get; init; }
        public int? Vibrance { get; init; }
        public string LightCrosshairVersion { get; init; } = string.Empty;

        public static NvidiaHelperRequest AuditProfile(string targetExePath) =>
            new() { Operation = NvidiaHelperOperations.AuditProfile, TargetExePath = targetExePath };

        public static NvidiaHelperRequest ApplyProfileSetting(
            string targetExePath,
            NvidiaProfileSettingWriteRequest request,
            string version) =>
            new()
            {
                Operation = NvidiaHelperOperations.ApplyProfileSetting,
                TargetExePath = targetExePath,
                ProfileWriteRequest = request,
                LightCrosshairVersion = version
            };

        public static NvidiaHelperRequest RestoreProfileSetting(
            string targetExePath,
            uint settingId,
            NvidiaProfileSettingBackup? backup) =>
            new()
            {
                Operation = NvidiaHelperOperations.RestoreProfileSetting,
                TargetExePath = targetExePath,
                SettingId = settingId,
                ProfileBackup = backup
            };

        public static NvidiaHelperRequest SetFpsCap(string targetExePath, int targetFps) =>
            new() { Operation = NvidiaHelperOperations.SetFpsCap, TargetExePath = targetExePath, TargetFps = targetFps };

        public static NvidiaHelperRequest ClearFpsCap(string targetExePath) =>
            new() { Operation = NvidiaHelperOperations.ClearFpsCap, TargetExePath = targetExePath };

        public static NvidiaHelperRequest SetVibrance(int vibrance) =>
            new() { Operation = NvidiaHelperOperations.SetVibrance, Vibrance = vibrance };
    }

    public sealed record NvidiaHelperResponse
    {
        public bool Success { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public NvidiaProfileAuditResult? AuditResult { get; init; }
        public NvidiaProfileSettingWriteResult? WriteResult { get; init; }

        public static NvidiaHelperResponse Error(string statusText) =>
            new() { Success = false, StatusText = statusText };

        public static NvidiaHelperResponse FromAudit(NvidiaProfileAuditResult result) =>
            new()
            {
                Success = result.Status is NvidiaProfileAuditStatus.Present or NvidiaProfileAuditStatus.NoProfile,
                StatusText = result.StatusText,
                AuditResult = result
            };

        public static NvidiaHelperResponse FromWrite(NvidiaProfileSettingWriteResult result) =>
            new()
            {
                Success = result.Succeeded,
                StatusText = result.StatusText,
                WriteResult = result
            };

        public static NvidiaHelperResponse FromBoolean(bool success, string statusText) =>
            new() { Success = success, StatusText = statusText };
    }

    public static class NvidiaHelperRequestValidator
    {
        public static NvidiaHelperResponse? ValidateBeforeDriver(NvidiaHelperRequest request)
        {
            if (request == null)
            {
                return NvidiaHelperResponse.Error("Missing NVIDIA helper request.");
            }

            return request.Operation switch
            {
                NvidiaHelperOperations.AuditProfile => ValidateTarget(request.TargetExePath, "auditing NVIDIA profile settings"),
                NvidiaHelperOperations.ApplyProfileSetting => ValidateApplyProfileRequest(request),
                NvidiaHelperOperations.RestoreProfileSetting => ValidateRestoreProfileRequest(request),
                NvidiaHelperOperations.SetFpsCap => ValidateSetFpsRequest(request),
                NvidiaHelperOperations.ClearFpsCap => ValidateTarget(request.TargetExePath, "clearing an NVIDIA driver FPS cap"),
                NvidiaHelperOperations.SetVibrance => ValidateVibranceRequest(request),
                _ => NvidiaHelperResponse.Error("Unknown NVIDIA helper operation.")
            };
        }

        private static NvidiaHelperResponse? ValidateApplyProfileRequest(NvidiaHelperRequest request)
        {
            if (request.ProfileWriteRequest == null)
            {
                return NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                    NvidiaProfileWriteStatus.NotAllowed,
                    "Missing NVIDIA profile setting write request.",
                    request.TargetExePath ?? string.Empty));
            }

            var validation = NvidiaProfileSettingWriteCatalog.ValidateRequest(
                request.TargetExePath,
                request.ProfileWriteRequest);
            if (!validation.IsValid)
            {
                return NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                    validation.Status,
                    validation.StatusText,
                    request.TargetExePath ?? string.Empty));
            }

            return null;
        }

        private static NvidiaHelperResponse? ValidateRestoreProfileRequest(NvidiaHelperRequest request)
        {
            var targetError = ValidateTarget(request.TargetExePath, "restoring NVIDIA profile controls");
            if (targetError != null)
            {
                return NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                    NvidiaProfileWriteStatus.InvalidTarget,
                    targetError.StatusText,
                    request.TargetExePath ?? string.Empty));
            }

            if (request.SettingId == null ||
                !NvidiaProfileSettingWriteCatalog.TryGet(request.SettingId.Value, out _))
            {
                return NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                    NvidiaProfileWriteStatus.NotAllowed,
                    "This NVIDIA profile setting is not restorable from LightCrosshair.",
                    request.TargetExePath ?? string.Empty));
            }

            if (request.ProfileBackup == null)
            {
                return NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                    NvidiaProfileWriteStatus.RestoreUnavailable,
                    "No LightCrosshair backup is available for this setting in the current session.",
                    request.TargetExePath ?? string.Empty));
            }

            return null;
        }

        private static NvidiaHelperResponse? ValidateSetFpsRequest(NvidiaHelperRequest request)
        {
            var targetError = ValidateTarget(request.TargetExePath, "applying an NVIDIA driver FPS cap");
            if (targetError != null)
            {
                return targetError;
            }

            int? targetFps = request.TargetFps;
            if (targetFps == null || targetFps < 15 || targetFps > 1000)
            {
                return NvidiaHelperResponse.Error("targetFps must be between 15 and 1000.");
            }

            return null;
        }

        private static NvidiaHelperResponse? ValidateVibranceRequest(NvidiaHelperRequest request)
        {
            int? vibrance = request.Vibrance;
            if (vibrance == null || vibrance < 0 || vibrance > 100)
            {
                return NvidiaHelperResponse.Error("Vibrance must be between 0 and 100.");
            }

            return null;
        }

        private static NvidiaHelperResponse? ValidateTarget(string? targetExePath, string action)
        {
            if (string.IsNullOrWhiteSpace(targetExePath))
            {
                return NvidiaHelperResponse.Error($"Select a target application before {action}.");
            }

            return null;
        }
    }
}
