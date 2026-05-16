#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace LightCrosshair.GpuDriver
{
    public interface INvidiaHelperClient
    {
        NvidiaHelperResponse Run(NvidiaHelperRequest request);
    }

    public interface INvidiaHelperProcessRunner
    {
        NvidiaHelperProcessResult Run(
            string executablePath,
            string responsePath,
            string requestJson,
            TimeSpan timeout);
    }

    public sealed record NvidiaHelperProcessResult(
        bool Started,
        bool TimedOut,
        int? ExitCode,
        string Stdout,
        string Stderr,
        string? ResponseText,
        string? StartError);

    public sealed class NvidiaHelperProcessClient : INvidiaHelperClient
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
        private readonly INvidiaHelperProcessRunner _runner;
        private readonly TimeSpan _timeout;
        private readonly Func<string?> _executablePathProvider;

        public NvidiaHelperProcessClient()
            : this(new NvidiaHelperProcessRunner(), DefaultTimeout, GetCurrentExecutablePath)
        {
        }

        public NvidiaHelperProcessClient(
            INvidiaHelperProcessRunner runner,
            TimeSpan timeout,
            Func<string?> executablePathProvider)
        {
            _runner = runner;
            _timeout = timeout;
            _executablePathProvider = executablePathProvider;
        }

        public NvidiaHelperResponse Run(NvidiaHelperRequest request)
        {
            var validationError = NvidiaHelperRequestValidator.ValidateBeforeDriver(request);
            if (validationError != null)
            {
                Program.LogLifecycle(
                    $"NVIDIA helper request rejected before process start: {SummarizeRequest(request)}; {validationError.StatusText}",
                    nameof(NvidiaHelperProcessClient));
                return validationError;
            }

            string? executablePath = _executablePathProvider();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return NvidiaHelperResponse.Error("NVIDIA helper executable path could not be resolved.");
            }

            string responsePath = Path.Combine(
                Path.GetTempPath(),
                $"LightCrosshair-nvidia-helper-{Guid.NewGuid():N}.json");
            string requestJson = JsonSerializer.Serialize(request, NvidiaHelperJson.Options);

            Program.LogLifecycle(
                $"Starting NVIDIA helper: {SummarizeRequest(request)}; helper={Path.GetFileName(executablePath)}",
                nameof(NvidiaHelperProcessClient));

            try
            {
                var processResult = _runner.Run(executablePath, responsePath, requestJson, _timeout);
                Program.LogLifecycle(
                    $"NVIDIA helper completed: {SummarizeRequest(request)}; started={processResult.Started}; timeout={processResult.TimedOut}; exit={processResult.ExitCode?.ToString() ?? "-"}; stdout={Truncate(processResult.Stdout)}; stderr={Truncate(processResult.Stderr)}",
                    nameof(NvidiaHelperProcessClient));

                if (!processResult.Started)
                {
                    return NvidiaHelperResponse.Error(processResult.StartError ?? "NVIDIA helper process did not start.");
                }

                if (processResult.TimedOut)
                {
                    return NvidiaHelperResponse.Error("NVIDIA helper timed out. The main UI remained isolated from the driver call.");
                }

                string? responseText = FirstNonWhiteSpace(processResult.ResponseText, processResult.Stdout);
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return NvidiaHelperResponse.Error(
                        $"NVIDIA helper exited without a structured result (exit code {processResult.ExitCode?.ToString() ?? "-"}).");
                }

                try
                {
                    var response = JsonSerializer.Deserialize<NvidiaHelperResponse>(responseText, NvidiaHelperJson.Options);
                    if (response == null)
                    {
                        return NvidiaHelperResponse.Error("NVIDIA helper returned an empty structured result.");
                    }

                    Program.LogLifecycle(
                        $"NVIDIA helper result: {SummarizeRequest(request)}; success={response.Success}; status={Truncate(response.StatusText)}",
                        nameof(NvidiaHelperProcessClient));
                    return response;
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "NvidiaHelperProcessClient.Deserialize");
                    return NvidiaHelperResponse.Error("NVIDIA helper returned an unreadable structured result.");
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "NvidiaHelperProcessClient.Run");
                return NvidiaHelperResponse.Error($"NVIDIA helper failed: {ex.Message}");
            }
            finally
            {
                TryDelete(responsePath);
            }
        }

        private static string? GetCurrentExecutablePath() =>
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

        private static string SummarizeRequest(NvidiaHelperRequest request)
        {
            string target = string.IsNullOrWhiteSpace(request.TargetExePath)
                ? "-"
                : Path.GetFileName(request.TargetExePath);
            string setting = request.ProfileWriteRequest?.SettingId.ToString("X8") ??
                             request.SettingId?.ToString("X8") ??
                             "-";
            string value = request.ProfileWriteRequest?.RawValue.ToString("X8") ??
                           request.TargetFps?.ToString() ??
                           request.Vibrance?.ToString() ??
                           "-";
            return $"operation={request.Operation}; target={target}; setting={setting}; value={value}";
        }

        private static string? FirstNonWhiteSpace(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string Truncate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            string compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return compact.Length <= 500 ? compact : compact[..500] + "...";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Temporary response cleanup is best-effort.
            }
        }
    }

    public sealed class NvidiaHelperProcessRunner : INvidiaHelperProcessRunner
    {
        public NvidiaHelperProcessResult Run(
            string executablePath,
            string responsePath,
            string requestJson,
            TimeSpan timeout)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add(NvidiaHelperHost.HelperArgument);
            process.StartInfo.ArgumentList.Add(NvidiaHelperHost.ResponseArgument);
            process.StartInfo.ArgumentList.Add(responsePath);

            try
            {
                if (!process.Start())
                {
                    return new NvidiaHelperProcessResult(false, false, null, string.Empty, string.Empty, null, "Process.Start returned false.");
                }
            }
            catch (Exception ex)
            {
                return new NvidiaHelperProcessResult(false, false, null, string.Empty, string.Empty, null, ex.Message);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(requestJson);
            process.StandardInput.Close();

            bool exited = process.WaitForExit((int)timeout.TotalMilliseconds);
            if (!exited)
            {
                TryKill(process);
                string timeoutStdout = ReadCompleted(stdoutTask);
                string timeoutStderr = ReadCompleted(stderrTask);
                return new NvidiaHelperProcessResult(true, true, null, timeoutStdout, timeoutStderr, ReadResponseFile(responsePath), null);
            }

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();
            return new NvidiaHelperProcessResult(true, false, process.ExitCode, stdout, stderr, ReadResponseFile(responsePath), null);
        }

        private static string ReadCompleted(System.Threading.Tasks.Task<string> task) =>
            task.IsCompletedSuccessfully ? task.Result : string.Empty;

        private static string? ReadResponseFile(string responsePath)
        {
            try
            {
                return File.Exists(responsePath) ? File.ReadAllText(responsePath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Timeout handling should return a structured error even if kill fails.
            }
        }
    }

    public static class NvidiaHelperJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }
}
