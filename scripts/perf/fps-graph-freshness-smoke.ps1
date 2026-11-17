# Frametime graph continuity: with a game presenting continuously, the newest graph sample must stay fresh
# (ETW buffers flushed every 50 ms) instead of arriving in one-second batches.
param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
. "$PSScriptRoot/ReviewProbe.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-graph-fresh-'+[guid]::NewGuid().ToString('N'))
$session=$null;$game=$null
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='graph-freshness'} | Out-Null
    $settings=(Invoke-ProbeCore $session getSnapshot).store.settings
    $settings.fpsOverlay.enabled=$true;$settings.fpsOverlay.showFrametimeGraph=$true
    Invoke-ProbeCore $session saveSettings $settings | Out-Null
    $ready=Join-Path $root 'game.ready'
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    $game=Start-Process ([IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")) -ArgumentList "--api 12 --borderless --seconds 12 --ready `"$ready`"" -PassThru -WindowStyle Normal
    Start-Sleep -Seconds 4
    $fps=[ReviewNative]::Find($session.App.Id,'LightCrosshair FPS')
    $ages=@(); $watch=[Diagnostics.Stopwatch]::StartNew()
    while($watch.Elapsed.TotalSeconds -lt 3){ $ages+=[int][PerfNative]::Query($fps,0x8073); Start-Sleep -Milliseconds 20 }
    $valid=@($ages | Where-Object { $_ -ge 0 })
    if($valid.Count -lt 50){throw "Graph had no data: $($ages -join ',')"}
    $max=($valid | Measure-Object -Maximum).Maximum; $avg=[math]::Round(($valid | Measure-Object -Average).Average,1)
    "Newest graph sample age over 3 s: average $avg ms, max $max ms ($($valid.Count) samples)"
    if($max -gt 250){throw "Graph data arrives in batches (newest sample up to $max ms old)"}
    'PASS: frametime graph data stays fresh, so the line scrolls continuously'
} finally {
    if($game){if(!$game.HasExited){$game.Kill()};$game.Dispose()}
    Stop-ProbeCore $session
}
