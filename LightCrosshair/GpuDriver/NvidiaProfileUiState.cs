#nullable enable

using System;

namespace LightCrosshair.GpuDriver
{
    public enum NvidiaProfileAuditTrigger
    {
        UiOpen,
        GpuTabSelected,
        ExplicitRefresh
    }

    public readonly record struct NvidiaProfileUiState(
        bool HasAuditResult,
        bool CanApplyProfileSettings,
        string StatusText)
    {
        public const string PassiveStatusText =
            "Select a target app and click Refresh to read NVIDIA profile settings.";

        public static NvidiaProfileUiState Initial() =>
            new(false, false, PassiveStatusText);

        public static bool ShouldAuditAutomatically(NvidiaProfileAuditTrigger trigger) =>
            trigger == NvidiaProfileAuditTrigger.ExplicitRefresh;

        public static NvidiaProfileUiState FromAuditResult(NvidiaProfileAuditResult result)
        {
            bool canApply = result.Status is NvidiaProfileAuditStatus.Present or NvidiaProfileAuditStatus.NoProfile;
            string status = result.Status switch
            {
                NvidiaProfileAuditStatus.Present => "Present",
                NvidiaProfileAuditStatus.NoProfile => "No profile",
                NvidiaProfileAuditStatus.InvalidTarget => "Invalid target",
                NvidiaProfileAuditStatus.Unsupported => "Unsupported",
                NvidiaProfileAuditStatus.Error => "Error",
                _ => result.Status.ToString()
            };

            return new(true, canApply, $"{status}: {result.StatusText}");
        }

        public static NvidiaProfileUiState RunExplicitAudit(
            string? targetApplicationPath,
            Func<NvidiaProfileAuditResult> audit)
        {
            try
            {
                return FromAuditResult(audit());
            }
            catch (AccessViolationException ex)
            {
                return DriverCallFailed(targetApplicationPath, ex);
            }
            catch (Exception ex)
            {
                return DriverCallFailed(targetApplicationPath, ex);
            }
        }

        public static NvidiaProfileUiState DriverCallFailed(string? targetApplicationPath, Exception ex)
        {
            string target = string.IsNullOrWhiteSpace(targetApplicationPath)
                ? "selected application"
                : targetApplicationPath.Trim();
            return new(
                true,
                false,
                $"NVIDIA driver call failed for {target}: {ex.Message}");
        }
    }
}
