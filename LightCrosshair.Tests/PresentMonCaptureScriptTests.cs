using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace LightCrosshair.Tests
{
    public class PresentMonCaptureScriptTests
    {
        [Fact]
        public void AnalyzePresentMonCaptureScript_ReportsVerifiedSignalOnlyForExplicitFrameType()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FPS,MsBetweenPresents,PresentMode,FrameType",
                "Sample.exe,120,8.33,Hardware: Independent Flip,Application",
                "Sample.exe,120,8.33,Hardware: Independent Flip,Generated");

            ScriptResult result = RunScript(csv);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Samples: 2", result.Output);
            Assert.Contains("Application: Sample.exe", result.Output);
            Assert.Contains("Frame Generation Classification: VerifiedSignalPresent", result.Output);
            Assert.Contains("FrameType explicitly reported 1 generated/interpolated sample(s).", result.Output);
        }

        [Fact]
        public void AnalyzePresentMonCaptureScript_KeepsCadenceWithoutFrameTypeHeuristicOnly()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FPS,MsBetweenPresents,PresentMode",
                "Sample.exe,120,16.67,Hardware: Independent Flip",
                "Sample.exe,120,8.33,Hardware: Independent Flip",
                "Sample.exe,120,16.67,Hardware: Independent Flip",
                "Sample.exe,120,8.33,Hardware: Independent Flip");

            ScriptResult result = RunScript(csv);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Frame Generation Classification: HeuristicOnly", result.Output);
            Assert.DoesNotContain("Frame Generation Classification: VerifiedSignalPresent", result.Output);
            Assert.Contains("Ratios/cadence are not verified evidence.", result.Output);
        }

        private static ScriptResult RunScript(string csv)
        {
            string root = FindRepoRoot();
            string scriptPath = Path.Combine(root, "scripts", "research", "analyze-presentmon-capture.ps1");
            string tempPath = Path.Combine(Path.GetTempPath(), $"lightcrosshair-presentmon-{Guid.NewGuid():N}.csv");
            File.WriteAllText(tempPath, csv);

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
                process.StartInfo.ArgumentList.Add("Bypass");
                process.StartInfo.ArgumentList.Add("-File");
                process.StartInfo.ArgumentList.Add(scriptPath);
                process.StartInfo.ArgumentList.Add("-CsvPath");
                process.StartInfo.ArgumentList.Add(tempPath);

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(15000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("PresentMon capture analysis script did not finish within 15 seconds.");
                }

                return new ScriptResult(process.ExitCode, output + error);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LightCrosshair.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find LightCrosshair repository root.");
        }

        private sealed record ScriptResult(int ExitCode, string Output);
    }
}
