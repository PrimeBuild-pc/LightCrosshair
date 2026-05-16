using System;
using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public class NvidiaHelperProcessClientTests
{
    [Fact]
    public void Run_InvalidTarget_ReturnsCleanErrorWithoutStartingHelper()
    {
        var runner = new FakeRunner();
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.AuditProfile(""));

        Assert.False(response.Success);
        Assert.Contains("Select a target application", response.StatusText);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public void Run_UnknownSettingValue_ReturnsCleanErrorWithoutStartingHelper()
    {
        var runner = new FakeRunner();
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.ApplyProfileSetting(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.LowLatencyModeSettingId, 2u),
            "1.7.0"));

        Assert.False(response.Success);
        Assert.NotNull(response.WriteResult);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, response.WriteResult!.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public void Run_GSyncWriteRejectedBeforeStartingHelper()
    {
        var runner = new FakeRunner();
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.ApplyProfileSetting(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.GSyncApplicationModeSettingId, 1u),
            "1.7.0"));

        Assert.False(response.Success);
        Assert.NotNull(response.WriteResult);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, response.WriteResult!.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public void Run_HelperTimeout_ReturnsCleanError()
    {
        var runner = new FakeRunner
        {
            Result = new NvidiaHelperProcessResult(true, true, null, "", "", null, null)
        };
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.AuditProfile(@"C:\Games\sample.exe"));

        Assert.False(response.Success);
        Assert.Contains("timed out", response.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public void Run_HelperCrashWithoutJson_ReturnsCleanError()
    {
        var runner = new FakeRunner
        {
            Result = new NvidiaHelperProcessResult(true, false, unchecked((int)0xC0000005), "", "Access violation", null, null)
        };
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.AuditProfile(@"C:\Games\sample.exe"));

        Assert.False(response.Success);
        Assert.Contains("without a structured result", response.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public void Run_HelperStructuredError_ReturnsHelperMessage()
    {
        var helperResponse = NvidiaHelperResponse.Error("NVIDIA driver call failed in helper.");
        var runner = new FakeRunner
        {
            Result = new NvidiaHelperProcessResult(
                true,
                false,
                0,
                "",
                "",
                System.Text.Json.JsonSerializer.Serialize(helperResponse, NvidiaHelperJson.Options),
                null)
        };
        var client = new NvidiaHelperProcessClient(runner, TimeSpan.FromSeconds(1), () => @"C:\Windows\System32\notepad.exe");

        var response = client.Run(NvidiaHelperRequest.AuditProfile(@"C:\Games\sample.exe"));

        Assert.False(response.Success);
        Assert.Equal("NVIDIA driver call failed in helper.", response.StatusText);
        Assert.Equal(1, runner.Calls);
    }

    private sealed class FakeRunner : INvidiaHelperProcessRunner
    {
        public int Calls { get; private set; }
        public NvidiaHelperProcessResult Result { get; init; } =
            new(true, false, 0, "", "", "{\"success\":true,\"statusText\":\"ok\"}", null);

        public NvidiaHelperProcessResult Run(
            string executablePath,
            string responsePath,
            string requestJson,
            TimeSpan timeout)
        {
            Calls++;
            return Result;
        }
    }
}
