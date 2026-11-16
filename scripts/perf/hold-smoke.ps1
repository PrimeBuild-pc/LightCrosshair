param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe", [int]$IdleBeforePressSeconds = 0)
. "$PSScriptRoot/ReviewProbe.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-final-hold-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$session=$null;$game=$null;$down=$false
$cursor=[ReviewNative+Point]::new();[void][ReviewNative]::GetCursorPos([ref]$cursor)
function Assert-Hold([bool]$visible) {
    $deadline=[DateTime]::UtcNow.AddSeconds(3)
    do { $snapshot=Invoke-ProbeCore $session getSnapshot; if($snapshot.runtime.crosshairShown -eq $visible){return $snapshot};Start-Sleep -Milliseconds 20 } while([DateTime]::UtcNow -lt $deadline)
    throw "Hold visibility did not become $visible; root=$root, foreground=$([ReviewNative]::GetForegroundWindow()), expected=$handle; runtime=$($snapshot.runtime | ConvertTo-Json -Compress -Depth 10)"
}
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='hold-smoke'} | Out-Null
    $snapshot=Invoke-ProbeCore $session getSnapshot
    $snapshot.store.settings.hotkeys.holdVisibility=@{key='RightClick';modifiers='none';mode='holdToHide'}
    Invoke-ProbeCore $session saveSettings $snapshot.store.settings | Out-Null
    $ready=Join-Path $root 'game.ready'
    $game=Start-Process -FilePath ([IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")) -ArgumentList "--api 12 --borderless --seconds 30 --ready `"$ready`"" -PassThru -WindowStyle Normal
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while(!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
    $handle=[IntPtr][long]((Get-Content $ready).Split(',')[1])
    if([ReviewNative]::GetForegroundWindow() -ne $handle){throw '3D probe must be foreground for the real button test'}
    if ($IdleBeforePressSeconds -gt 0) { Start-Sleep -Seconds $IdleBeforePressSeconds }
    [void][ReviewNative]::SetCursorPos(500,400)
    $window=[PerfNative]::Find($session.App.Id,$false)
    Start-Sleep -Milliseconds 200
    if([PerfNative]::Query($window,0x8070) -ne 1){throw 'Hold should install only the mouse hook'}
    $before=@($session.Events | Where-Object method -eq 'snapshotChanged').Count
    [ReviewNative]::RightButton($true);$down=$true
    $snapshot=Assert-Hold $false
    if(!$snapshot.store.settings.crosshairVisible){throw 'Hold was persisted'}
    [ReviewNative]::RightButton($false);$down=$false
    Assert-Hold $true | Out-Null
    if([PerfNative]::Query($handle,0x8066) -lt 1){throw 'The right click did not reach the game'}
    if(@($session.Events | Where-Object method -eq 'snapshotChanged').Count -ne $before){throw 'A transient hold wrote a store snapshot'}
    [ReviewNative]::RightButton($true);$down=$true;Assert-Hold $false | Out-Null
    Invoke-ProbeCore $session setInputCapture @{active=$true} | Out-Null
    Assert-Hold $true | Out-Null
    [ReviewNative]::RightButton($false);$down=$false
    Invoke-ProbeCore $session setInputCapture @{active=$false} | Out-Null
    Invoke-ProbeCore $session saveGameProfile @{id='hold-probe';name='Hold probe';processName='swapchainprobe.exe';crosshairId=$snapshot.store.settings.defaultCrosshairId;holdVisibility=@{key='';mode='holdToHide'}} | Out-Null
    Start-Sleep -Milliseconds 200
    if([PerfNative]::Query($window,0x8070) -ne 0){throw 'Off in this game must remove the hold hook'}
    Write-Host "PASS: real right hold/release, pass-through click, no persistence, capture reset and per-game hook removal ($root)"
} finally {
    if($down){[ReviewNative]::RightButton($false)}
    [void][ReviewNative]::SetCursorPos($cursor.X,$cursor.Y)
    if($game){if(!$game.HasExited){[void]$game.CloseMainWindow();if(!$game.WaitForExit(5000)){$game.Kill()}};$game.Dispose()}
    Stop-ProbeCore $session
}
