# Interactive probe: focus only owned windows and click the observed saved card.
param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
. "$PSScriptRoot/ReviewProbe.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-final-use-'+[guid]::NewGuid().ToString('N'))
$session=$null;$game=$null;$ui=$null;$released=$false
$cursor=[ReviewNative+Point]::new();[void][ReviewNative]::GetCursorPos([ref]$cursor)
try{
 $session=Start-ProbeCore $Executable $root -ProductionPipe
 Invoke-ProbeCore $session hello @{protocolVersion=1;client='use-ui-smoke'} | Out-Null
 $s=Invoke-ProbeCore $session getSnapshot;$initial=$s.store.settings.defaultCrosshairId
 $extra=$s.store.crosshairs[0].psobject.Copy();$extra.id=[guid]::NewGuid().ToString('N');$extra.name='Final Use alternate'
 Invoke-ProbeCore $session saveCrosshair $extra | Out-Null
 Invoke-ProbeCore $session saveGameProfile @{id='use-probe';name='3D test';processName='swapchainprobe.exe';crosshairId=$initial} | Out-Null
 $game=Start-Process ([IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")) -ArgumentList '--api 12 --borderless --seconds 120' -PassThru -WindowStyle Normal
 Start-Sleep -Seconds 1
 $focus=agent-desktop --headed focus-window --app SwapchainProbe | ConvertFrom-Json
 if(!$focus.ok){throw 'Cannot focus the 3D test window'}
 $deadline=[DateTime]::UtcNow.AddSeconds(5)
 do{$context=Invoke-ProbeCore $session getSnapshot;if($context.runtime.activeGameProfileId -eq 'use-probe'){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 if($context.runtime.activeGameProfileId -ne 'use-probe'){throw 'Game must be active before UI opens'}
 $session.Reader.Dispose();$session.Pipe.Dispose();$released=$true
 $previousRoot=$env:LIGHTCROSSHAIR_DATA_ROOT
 try{$env:LIGHTCROSSHAIR_DATA_ROOT=$session.Root;$ui=Start-Process ([IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $Executable) 'LightCrosshair.UI.exe'))) -ArgumentList '--page library' -PassThru -WindowStyle Normal}
 finally{$env:LIGHTCROSSHAIR_DATA_ROOT=$previousRoot}
 $info=@{Root=$root;CorePid=$session.App.Id;UiPid=$ui.Id;DefaultId=$initial;AlternateId=$extra.id};$info|ConvertTo-Json|Set-Content "$PSScriptRoot/results-final/use-ui.json";$info|ConvertTo-Json
 function Find-Card($node) { if($node.role -eq 'option' -and $node.name -eq 'Final Use alternate'){$node}; foreach($child in $node.children){Find-Card $child} }
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 do {
  $tree=agent-desktop snapshot --app LightCrosshair.UI --include-bounds --max-depth 16 | ConvertFrom-Json
  if(!$tree.ok){Start-Sleep -Milliseconds 100; continue}
  $cards=@(Find-Card $tree.data.tree)
 }until($cards.Count -eq 1 -or [DateTime]::UtcNow -gt $deadline)
 if($cards.Count -ne 1){throw 'Test card is not uniquely visible'}
 $uiWindow=[ReviewNative]::Find($ui.Id,'LightCrosshair')
 $gameWindow=[ReviewNative]::Find($game.Id,'LightCrosshair DX12 probe')
 if([ReviewNative]::GetForegroundWindow() -ne $gameWindow){throw 'Only the owned 3D window may transfer foreground'}
 if(![ReviewNative]::TransferProbeFocus($gameWindow,$uiWindow)){throw 'The test window did not transfer foreground to UI'}
 Start-Sleep -Milliseconds 150
 if([ReviewNative]::GetForegroundWindow() -ne $uiWindow){throw 'UI did not accept foreground'}
 $bounds=$cards[0].bounds
 $xy="$([int]($bounds.x+$bounds.width/2)),$([int]($bounds.y+65))"
 $click=agent-desktop --headed mouse-click --xy $xy | ConvertFrom-Json
 if(!$click.ok){throw ($click.error|ConvertTo-Json -Compress)}
 do {
  $store=Get-Content -LiteralPath (Join-Path $session.Root 'store.json') -Raw | ConvertFrom-Json
  if($store.gameProfiles[0].crosshairId -eq $extra.id){break};Start-Sleep -Milliseconds 50
 }while([DateTime]::UtcNow -lt $deadline)
 if($store.gameProfiles[0].crosshairId -ne $extra.id -or $store.settings.defaultCrosshairId -ne $initial){throw 'Native UI Use did not update only the game context'}
 $verified=agent-desktop snapshot --app LightCrosshair.UI --max-depth 16 | ConvertFrom-Json
 if(!$verified.ok){throw 'Cannot inspect UI after Use'}
 'PASS: real UI Use changed the foreground game and preserved the default.'
}finally{
 [void][ReviewNative]::SetCursorPos($cursor.X,$cursor.Y)
 if($ui){if(!$ui.HasExited){$ui.Kill();$ui.WaitForExit()};$ui.Dispose()}
 if($game){if(!$game.HasExited){[void]$game.CloseMainWindow();if(!$game.WaitForExit(3000)){$game.Kill()}};$game.Dispose()}
 if($session -and $released){[void][PerfNative]::PostMessage([PerfNative]::Find($session.App.Id,$false),0x10,[IntPtr]::Zero,[IntPtr]::Zero);if(!$session.App.WaitForExit(10000)){$session.App.Kill()};$session.App.Dispose()}
 else{Stop-ProbeCore $session}
}
