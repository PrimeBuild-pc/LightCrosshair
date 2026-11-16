param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",[string]$OutputDirectory="$PSScriptRoot/results-final/fps-graph")
. "$PSScriptRoot/ReviewProbe.ps1"
$out=[IO.Path]::GetFullPath($OutputDirectory);New-Item -ItemType Directory -Path $out -Force | Out-Null
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-final-graph-'+[guid]::NewGuid().ToString('N'))
$session=$null;$game=$null;$rows=@()
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='fps-graph-smoke'} | Out-Null
    $settings=(Invoke-ProbeCore $session getSnapshot).store.settings
    $settings.fpsOverlay.enabled=$true;$settings.fpsOverlay.showFrametimeGraph=$false
    Invoke-ProbeCore $session saveSettings $settings | Out-Null
    $ready=Join-Path $out 'game.ready';if(Test-Path -LiteralPath $ready){Remove-Item -LiteralPath $ready}
    $game=Start-Process ([IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")) -ArgumentList "--api 12 --borderless --seconds 30 --pause-after 2 --ready `"$ready`"" -PassThru -WindowStyle Normal
    Start-Sleep -Seconds 6
    $fps=[ReviewNative]::Find($session.App.Id,'LightCrosshair FPS')
    $core=[PerfNative]::Find($session.App.Id,$false)
    $static=[PerfNative]::Query($core,0x8064)
    foreach($graph in $false,$true,$false){
        $settings=(Invoke-ProbeCore $session getSnapshot).store.settings
        $settings.fpsOverlay.showFrametimeGraph=$graph
        Invoke-ProbeCore $session saveSettings $settings | Out-Null
        Start-Sleep -Milliseconds 500
        $before=[PerfNative]::Query($fps,0x8064);$watch=[Diagnostics.Stopwatch]::StartNew()
        Start-Sleep -Seconds 2
        $count=[PerfNative]::Query($fps,0x8064)-$before;$seconds=$watch.Elapsed.TotalSeconds
        $rows += [pscustomobject]@{Graph=$graph;Seconds=$seconds;Redraws=$count;Hz=$count/$seconds}
        if($graph -and ($count -lt 20 -or $count -gt [math]::Ceiling($seconds*20)+1)){throw 'Graph cadence must stay at or below 20 Hz'}
        if(!$graph -and $count -ne 0){throw 'Graph-off overlay redraws continuously without changing data'}
    }
    $settings.fpsOverlay.enabled=$false;Invoke-ProbeCore $session saveSettings $settings | Out-Null
    if([PerfNative]::Query($core,0x8067) -ne 0){throw 'An FPS timer remains enabled while off'}
    if([PerfNative]::Query($core,0x8064) -ne $static){throw 'Static crosshair redrew during FPS changes'}
    Write-Host 'PASS: graph cadence bounded, graph-off stable overlay has zero redraws, FPS-off has zero timers, static crosshair has zero redraws'
    $rows | Export-Csv (Join-Path $out 'cadence.csv') -NoTypeInformation
    $rows | Format-Table
} finally {
    if($game){if(!$game.HasExited){[void]$game.CloseMainWindow();if(!$game.WaitForExit(5000)){$game.Kill()}};$game.Dispose()}
    Stop-ProbeCore $session
}
