param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [string]$Probe = "$PSScriptRoot/results-review3/probe/SwapchainProbe.exe"
)
. "$PSScriptRoot/ReviewProbe.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc3-overlay-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$session=$null; $game=$null; $other=$null
function Assert-Overlay($s, [IntPtr]$foreground, [bool]$shown) {
    $deadline=[DateTime]::UtcNow.AddSeconds(4)
    do { $snapshot=Invoke-ProbeCore $s getSnapshot; if($snapshot.runtime.crosshairShown -eq $shown){break}; Start-Sleep -Milliseconds 50 } while([DateTime]::UtcNow -lt $deadline)
    $window=[PerfNative]::Find($s.App.Id,$false)
    if($snapshot.runtime.crosshairShown -ne $shown -or [ReviewNative]::IsWindowVisible($window) -ne $shown){throw 'Overlay visibility mismatch'}
    if($shown -and ![ReviewNative]::Above($window,$foreground)){throw 'Overlay is below the 3D window'}
    return $snapshot
}
function Press-AltX {
    # This probe explicitly exercises the real global shortcut with the test game
    # foreground, as requested; activation is part of the focus-change test.
    $result=agent-desktop --headed press 'alt+x' --app SwapchainProbe | ConvertFrom-Json
    if(!$result.ok){throw ($result.error | ConvertTo-Json -Compress)}
}
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='game-overlay-smoke'} | Out-Null
    $snapshot=Invoke-ProbeCore $session getSnapshot
    $initial=$snapshot.store.settings.defaultCrosshairId
    $extra=$snapshot.store.crosshairs[0].PSObject.Copy();$extra.id=[guid]::NewGuid().ToString('N');$extra.name='Game cycling alternate'
    Invoke-ProbeCore $session saveCrosshair $extra | Out-Null
    Invoke-ProbeCore $session saveGameProfile @{id='probe';name='3D probe';processName='swapchainprobe.exe';crosshairId=$initial;enabled=$true} | Out-Null
    $ready=Join-Path $root 'game.ready'
    $game=Start-Process -FilePath ([IO.Path]::GetFullPath($Probe)) -ArgumentList "--api 12 --borderless --seconds 60 --ready `"$ready`"" -PassThru -WindowStyle Normal
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while(!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
    $handle=[IntPtr][long]((Get-Content -LiteralPath $ready).Split(',')[1])
    [void][ReviewNative]::SetForegroundWindow($handle);Start-Sleep -Milliseconds 200
    $snapshot=Assert-Overlay $session $handle $true
    if($snapshot.runtime.activeGameProfileId -ne 'probe'){throw 'Probe game context not active'}
    Press-AltX; Assert-Overlay $session $handle $false | Out-Null
    Press-AltX; Assert-Overlay $session $handle $true | Out-Null
    $focus=agent-desktop --headed focus-window --app SwapchainProbe | ConvertFrom-Json
    if(!$focus.ok){throw ($focus.error | ConvertTo-Json -Compress)}
    Start-Sleep -Milliseconds 150
    $snapshot=Invoke-ProbeCore $session getSnapshot
    if($snapshot.runtime.activeGameProfileId -ne 'probe'){throw 'Game lost foreground before cycling'}
    $coreWindow=[PerfNative]::Find($session.App.Id,$false)
    [void][PerfNative]::PostMessage($coreWindow,0x312,[IntPtr]9001,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $snapshot=Invoke-ProbeCore $session getSnapshot
    if($snapshot.store.gameProfiles[0].crosshairId -ne $extra.id -or $snapshot.store.settings.defaultCrosshairId -ne $initial){throw ('Foreground game cycle did not persist independently: '+($snapshot.runtime | ConvertTo-Json -Depth 8 -Compress))}
    $ready2=Join-Path $root 'other.ready'
    $other=Start-Process -FilePath ([IO.Path]::GetFullPath($Probe)) -ArgumentList "--api 11 --seconds 15 --ready `"$ready2`"" -PassThru -WindowStyle Normal
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while(!(Test-Path -LiteralPath $ready2) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
    $handle2=[IntPtr][long]((Get-Content -LiteralPath $ready2).Split(',')[1])
    foreach($target in $handle2,$handle,$handle2,$handle){
        $from=[ReviewNative]::GetForegroundWindow()
        if($from -ne $handle -and $from -ne $handle2){throw 'A non-test window has foreground; do not send it probe messages'}
        if($from -ne $target -and ![ReviewNative]::TransferProbeFocus($from,$target)){throw 'Test window did not transfer foreground'}
        Start-Sleep -Milliseconds 150
        if([ReviewNative]::GetForegroundWindow() -ne $target){throw 'Focus change failed'}
        Assert-Overlay $session $target $true | Out-Null
    }
    Write-Host "PASS: DX12 borderless topmost, real Alt+X off/on, four focus changes, foreground-game cycle persistence ($root)"
} finally {
    foreach($process in $other,$game){if($process){if(!$process.HasExited){[void]$process.CloseMainWindow();if(!$process.WaitForExit(5000)){$process.Kill()}};$process.Dispose()}}
    Stop-ProbeCore $session
}
