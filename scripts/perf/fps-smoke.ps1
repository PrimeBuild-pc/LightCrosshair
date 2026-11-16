param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [string]$Probe = "$PSScriptRoot/results-review3/probe/SwapchainProbe.exe",
    [string]$OutputDirectory = "$PSScriptRoot/results-review3/fps-after",
    [switch]$Baseline,
    [switch]$Screenshots
)
. "$PSScriptRoot/ReviewProbe.ps1"
$out = [IO.Path]::GetFullPath($OutputDirectory); New-Item -ItemType Directory -Path $out -Force | Out-Null
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc3-fps-' + [Guid]::NewGuid().ToString('N'))
$session = $null; $probeProcess = $null; $rows = @()
try {
    $session = Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='fps-smoke'} | Out-Null
    $snapshot = Invoke-ProbeCore $session getSnapshot
    $snapshot.store.settings.fpsOverlay.enabled = $true
    Invoke-ProbeCore $session saveSettings $snapshot.store.settings | Out-Null
    foreach ($api in 11,12) { foreach ($borderless in $false,$true) {
        $name = "dx$api-" + $(if ($borderless) {'borderless'} else {'windowed'})
        $ready = Join-Path $out "$name.ready"; if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready }
        $frames = Join-Path $out "$name-frames.csv"
        $arguments = "--api $api --seconds 8 --pause-after 5 --output `"$frames`" --ready `"$ready`""
        if ($borderless) { $arguments += ' --borderless' }
        $probeProcess = Start-Process -FilePath ([IO.Path]::GetFullPath($Probe)) -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $deadline = [DateTime]::UtcNow.AddSeconds(5)
        while (!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline -and !$probeProcess.HasExited) { Start-Sleep -Milliseconds 25 }
        if (!(Test-Path -LiteralPath $ready)) { throw "$name did not initialize" }
        $handle = [IntPtr][long]((Get-Content -LiteralPath $ready).Split(',')[1])
        [void][ReviewNative]::SetForegroundWindow($handle)
        Start-Sleep -Seconds 3
        $snapshot = Invoke-ProbeCore $session getSnapshot
        $window = [ReviewNative]::Find($session.App.Id, 'LightCrosshair FPS')
        $fps = [PerfNative]::Query($window, 0x806A) / 100.0
        $reason = [PerfNative]::Query($window, 0x806B)
        $foreground = $snapshot.runtime.foregroundProcess
        $probeFrames = [PerfNative]::Query($handle, 0x8064) # WM_APP+100 == 0x8064
        if ($Screenshots) {
            agent-desktop screenshot --screen 0 (Join-Path $out "$name.png") | Out-Null
        }
        Start-Sleep -Milliseconds 4400
        $staleFps = [PerfNative]::Query($window, 0x806A) / 100.0
        $staleReason = [PerfNative]::Query($window, 0x806B)
        $rows += [pscustomobject]@{Case=$name;Foreground=$foreground;ProbeFrames=$probeFrames;Fps=$fps;Reason=$reason;StaleFps=$staleFps;StaleReason=$staleReason;Visible=[ReviewNative]::IsWindowVisible($window)}
        if (!$probeProcess.WaitForExit(5000)) { throw "$name did not exit" }; $probeProcess.Dispose(); $probeProcess = $null
        if (!$Baseline -and ($fps -le 0 -or $staleFps -ne 0 -or $staleReason -eq 0 -or $foreground -ne 'swapchainprobe.exe')) { throw "${name}: telemetry or stale-state verification failed" }
    } }
} finally {
    $rows | Export-Csv (Join-Path $out 'matrix.csv') -NoTypeInformation
    if ($probeProcess) { if (!$probeProcess.HasExited) { $probeProcess.Kill() }; $probeProcess.Dispose() }
    Stop-ProbeCore $session
}
$rows | Format-Table
Write-Host "FPS matrix: $out; isolated data: $root"
