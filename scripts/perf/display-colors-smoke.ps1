param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [string]$OutputDirectory = "$PSScriptRoot/results-review3/colors-after",
    [int]$Gamma = 100,
    [switch]$Baseline
)
. "$PSScriptRoot/ReviewProbe.ps1"
$out=[IO.Path]::GetFullPath($OutputDirectory);New-Item -ItemType Directory -Path $out -Force | Out-Null
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc3-colors-'+[guid]::NewGuid().ToString('N'))
dotnet build "$PSScriptRoot/ColorProbe" -c Release --nologo
if($LASTEXITCODE){throw 'Color snapshot probe build failed'}
$snapshotExe="$PSScriptRoot/ColorProbe/bin/Release/net10.0-windows/ColorProbe.exe"
$before=Join-Path $out 'before.json';$after=Join-Path $out 'after.json'
& $snapshotExe $before (Join-Path $root 'snapshot-logs')
if($LASTEXITCODE){throw 'Cannot capture original display colors; no changes attempted'}
$session=$null;$game=$null;$during=$null;$left=$null
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='color-smoke'} | Out-Null
    $snapshot=Invoke-ProbeCore $session getSnapshot
    Invoke-ProbeCore $session saveGameProfile @{id='probe';name='Display colors probe';processName='swapchainprobe.exe';crosshairId=$snapshot.store.settings.defaultCrosshairId;enabled=$true;displayColorsEnabled=$true;displayColors=@{gamma=$Gamma;contrast=100;brightness=100;vibrance=50};fpsOverlay=$false} | Out-Null
    $ready=Join-Path $out 'game.ready';if(Test-Path -LiteralPath $ready){Remove-Item -LiteralPath $ready}
    $probe=[IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")
    $game=Start-Process $probe -ArgumentList "--api 12 --borderless --seconds 15 --ready `"$ready`"" -PassThru -WindowStyle Normal
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    do { Start-Sleep -Milliseconds 100;$during=Invoke-ProbeCore $session getSnapshot } while($during.runtime.activeGameProfileId -ne 'probe' -and [DateTime]::UtcNow -lt $deadline)
    if($during.runtime.activeGameProfileId -ne 'probe'){throw 'Color test game did not become foreground'}
    Start-Sleep -Seconds 2
    $during=Invoke-ProbeCore $session getSnapshot
    [void]$game.CloseMainWindow();if(!$game.WaitForExit(5000)){throw '3D test did not exit'};$game.Dispose();$game=$null
    Start-Sleep -Seconds 1
    $left=Invoke-ProbeCore $session getSnapshot
    if(!$Baseline -and $left.runtime.displayColorsError){throw 'Display color error did not clear on leave'}
} finally {
    if($game){if(!$game.HasExited){[void]$game.CloseMainWindow();if(!$game.WaitForExit(5000)){$game.Kill()}};$game.Dispose()}
    Stop-ProbeCore $session
    & $snapshotExe $after (Join-Path $root 'snapshot-logs')
    if($LASTEXITCODE){throw 'Cannot verify display-color restoration'}
    if((Get-FileHash $before).Hash -ne (Get-FileHash $after).Hash){throw "Display colors differ from baseline: $before / $after"}
    if(Test-Path -LiteralPath (Join-Path $root 'v2/colors-applied.json')){throw 'Color recovery marker remains after shutdown'}
}
$log=Join-Path $root 'v2/logs/core.log'
if(Test-Path -LiteralPath $log){Copy-Item -LiteralPath $log -Destination (Join-Path $out 'core.log')}
$failures=if(Test-Path -LiteralPath $log){@(Get-Content -LiteralPath $log | Select-String 'Display color transaction').Count}else{0}
if(!$Baseline -and $failures -gt 0 -and !$during.runtime.displayColorsError){throw 'Color apply failed silently without RuntimeState.DisplayColorsError'}
if(!$Baseline -and $failures -gt 1){throw 'Vendor failure was logged more than once'}
[pscustomobject]@{Gamma=$Gamma;DuringError=$during.runtime.displayColorsError;AfterLeaveError=$left.runtime.displayColorsError;FailureLogEntries=$failures;OriginalColorsRestored=$true;DataRoot=$root} | ConvertTo-Json | Set-Content (Join-Path $out 'summary.json')
Get-Content (Join-Path $out 'summary.json')
if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log | Select-String 'Display color|Color apply|AMD.*failed|unsupported|not supported'}
