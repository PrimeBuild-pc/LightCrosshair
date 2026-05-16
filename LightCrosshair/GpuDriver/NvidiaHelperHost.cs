#nullable enable

using System;
using System.IO;
using System.Text.Json;

namespace LightCrosshair.GpuDriver
{
    public static class NvidiaHelperHost
    {
        public const string HelperArgument = "--nvidia-helper";
        public const string ResponseArgument = "--response";

        public static bool IsHelperInvocation(string[] args) =>
            Array.Exists(args, item => string.Equals(item, HelperArgument, StringComparison.OrdinalIgnoreCase));

        public static int Run(string[] args)
        {
            string? responsePath = TryGetResponsePath(args);

            try
            {
                Program.LogLifecycle("NVIDIA helper process started.", nameof(NvidiaHelperHost));
                string requestJson = Console.In.ReadToEnd();
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    WriteResponse(responsePath, NvidiaHelperResponse.Error("NVIDIA helper received an empty request."));
                    return 2;
                }

                var request = JsonSerializer.Deserialize<NvidiaHelperRequest>(requestJson, NvidiaHelperJson.Options);
                if (request == null)
                {
                    WriteResponse(responsePath, NvidiaHelperResponse.Error("NVIDIA helper could not read the request."));
                    return 2;
                }

                var response = Execute(request);
                WriteResponse(responsePath, response);
                Program.LogLifecycle(
                    $"NVIDIA helper process finished: operation={request.Operation}; success={response.Success}",
                    nameof(NvidiaHelperHost));
                return 0;
            }
            catch (AccessViolationException ex)
            {
                var response = NvidiaHelperResponse.Error($"NVIDIA driver call failed: {ex.Message}");
                WriteResponse(responsePath, response);
                return 3;
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "NvidiaHelperHost.Run");
                WriteResponse(responsePath, NvidiaHelperResponse.Error($"NVIDIA helper failed: {ex.Message}"));
                return 2;
            }
        }

        private static NvidiaHelperResponse Execute(NvidiaHelperRequest request)
        {
            var validationError = NvidiaHelperRequestValidator.ValidateBeforeDriver(request);
            if (validationError != null)
            {
                return validationError;
            }

            var service = new NvidiaDriverService();
            if (!service.TryInitializeForIsolatedOperation(out string initError))
            {
                return request.Operation switch
                {
                    NvidiaHelperOperations.ApplyProfileSetting or NvidiaHelperOperations.RestoreProfileSetting =>
                        NvidiaHelperResponse.FromWrite(NvidiaProfileSettingWriteResult.FromStatus(
                            NvidiaProfileWriteStatus.Unsupported,
                            initError,
                            request.TargetExePath ?? string.Empty)),
                    NvidiaHelperOperations.AuditProfile =>
                        NvidiaHelperResponse.FromAudit(NvidiaProfileAuditResult.Unsupported(initError)),
                    _ => NvidiaHelperResponse.Error(initError)
                };
            }

            return request.Operation switch
            {
                NvidiaHelperOperations.AuditProfile =>
                    NvidiaHelperResponse.FromAudit(service.AuditNvidiaProfileSettings(request.TargetExePath)),
                NvidiaHelperOperations.ApplyProfileSetting =>
                    NvidiaHelperResponse.FromWrite(service.ApplyNvidiaProfileSetting(
                        request.TargetExePath,
                        request.ProfileWriteRequest!,
                        request.LightCrosshairVersion)),
                NvidiaHelperOperations.RestoreProfileSetting =>
                    NvidiaHelperResponse.FromWrite(service.RestoreNvidiaProfileSettingFromBackup(request.ProfileBackup!)),
                NvidiaHelperOperations.SetFpsCap =>
                    RunFpsCapApply(service, request),
                NvidiaHelperOperations.ClearFpsCap =>
                    RunFpsCapClear(service, request),
                NvidiaHelperOperations.SetVibrance =>
                    RunVibranceApply(service, request),
                _ => NvidiaHelperResponse.Error("Unknown NVIDIA helper operation.")
            };
        }

        private static NvidiaHelperResponse RunFpsCapApply(NvidiaDriverService service, NvidiaHelperRequest request)
        {
            bool success = service.TrySetNvidiaFpsCap(request.TargetFps!.Value, request.TargetExePath, out string error);
            return NvidiaHelperResponse.FromBoolean(
                success,
                success
                    ? $"FPS cap of {request.TargetFps.Value} applied successfully to '{request.TargetExePath}'."
                    : $"Failed to apply FPS cap: {error}");
        }

        private static NvidiaHelperResponse RunFpsCapClear(NvidiaDriverService service, NvidiaHelperRequest request)
        {
            bool success = service.TryClearNvidiaFpsCap(request.TargetExePath, out string error);
            return NvidiaHelperResponse.FromBoolean(
                success,
                success
                    ? $"FPS cap cleared for '{request.TargetExePath}'."
                    : $"Failed to clear FPS cap: {error}");
        }

        private static NvidiaHelperResponse RunVibranceApply(NvidiaDriverService service, NvidiaHelperRequest request)
        {
            bool success = service.TrySetNvidiaVibrance(request.Vibrance!.Value, out string error);
            return NvidiaHelperResponse.FromBoolean(
                success,
                success
                    ? $"Digital vibrance set to {request.Vibrance.Value}."
                    : $"Failed to set vibrance: {error}");
        }

        private static void WriteResponse(string? responsePath, NvidiaHelperResponse response)
        {
            string json = JsonSerializer.Serialize(response, NvidiaHelperJson.Options);
            if (!string.IsNullOrWhiteSpace(responsePath))
            {
                try
                {
                    File.WriteAllText(responsePath, json);
                }
                catch
                {
                    // stdout still carries the structured response.
                }
            }

            Console.Out.Write(json);
            Console.Out.Flush();
        }

        private static string? TryGetResponsePath(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], ResponseArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
