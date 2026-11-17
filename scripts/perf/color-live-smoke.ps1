# Display colors (open item 7): a simulated slider drag must never flash the original ramp, the released value
# must stay, and a profile saved while its game is foreground must apply at once. Ends with an exact restoration check.
param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",[string]$OutputDirectory="$PSScriptRoot/results-final/color-live")
. "$PSScriptRoot/ReviewProbe.ps1"
Add-Type -AssemblyName System.Windows.Forms
$out=[IO.Path]::GetFullPath($OutputDirectory);New-Item -ItemType Directory -Force -Path $out | Out-Null
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-color-live-'+[guid]::NewGuid().ToString('N'))
dotnet build "$PSScriptRoot/ColorProbe" -c Release --nologo -v q | Out-Null
if($LASTEXITCODE){throw 'Readback helper build failed'}
$reader=[IO.Path]::GetFullPath("$PSScriptRoot/ColorProbe/bin/Release/net10.0-windows/ColorProbe.exe")
$displays=@([System.Windows.Forms.Screen]::AllScreens | ForEach-Object DeviceName)
function Read-Colors([string]$Name){& $reader (Join-Path $out ($Name+'.json')) (Join-Path $root 'readback') | Out-Null;if($LASTEXITCODE){throw 'Readback failed'};Get-Content (Join-Path $out ($Name+'.json')) -Raw | ConvertFrom-Json}
function Red($snapshot){@($snapshot.Displays.psobject.Properties | ForEach-Object { $_.Value.Red[128] })}
function Same([string]$A,[string]$B){(Get-FileHash (Join-Path $out ($A+'.json'))).Hash -eq (Get-FileHash (Join-Path $out ($B+'.json'))).Hash}
$session=$null;$game=$null;$result=[ordered]@{}
$original=Read-Colors original
$originalRed=Red $original
try {
    $session=Start-ProbeCore $Executable $root;Invoke-ProbeCore $session hello @{protocolVersion=1;client='color-live'} | Out-Null
    # 1. Drag: 15 preview steps at the UI's 50 ms spacing while the ramp is sampled continuously.
    $csv=Join-Path $out 'drag.csv'
    $watcher=Start-Process $reader -ArgumentList (@('--watch','2.4',"`"$csv`"")+$displays) -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 300
    foreach($i in 1..15){ Invoke-ProbeCore $session previewDisplayColors @{colors=@{gamma=(100+3*$i);contrast=(100+$i);brightness=100;vibrance=(50+2*$i)}} | Out-Null; Start-Sleep -Milliseconds 50 }
    $watcher.WaitForExit()
    $column=$displays[0]
    $values=@(Import-Csv $csv | ForEach-Object { [int]$_.$column })
    $flashes=0;$changed=$false
    for($k=1;$k -lt $values.Count;$k++){ if($values[$k] -ne $originalRed[0]){$changed=$true} elseif($changed -and $values[$k-1] -ne $originalRed[0]){$flashes++} }
    $result.DragSamples=$values.Count; $result.DistinctRampValues=@($values | Select-Object -Unique).Count; $result.FlashesBackToOriginal=$flashes
    # 2. Release (Settings page): the final value is saved, then the preview ends. The released value must stay.
    Read-Colors dragged | Out-Null
    $settings=(Invoke-ProbeCore $session getSnapshot).store.settings
    $settings | Add-Member displayColorsEnabled $true -Force
    $settings | Add-Member displayColors @{gamma=145;contrast=115;brightness=100;vibrance=80} -Force
    Invoke-ProbeCore $session saveSettings $settings | Out-Null
    Invoke-ProbeCore $session previewDisplayColors @{colors=$null} | Out-Null; Start-Sleep -Milliseconds 400
    Read-Colors released | Out-Null
    $result.ReleasedValueKept=(Same dragged released)
    # 3. Game in foreground: a saved profile applies at once, no restart.
    $id=(Invoke-ProbeCore $session getSnapshot).store.settings.defaultCrosshairId
    Invoke-ProbeCore $session saveGameProfile @{id='live';name='Live';processName='swapchainprobe.exe';crosshairId=$id;displayColorsEnabled=$true;displayColors=@{gamma=110;contrast=100;brightness=100;vibrance=50}} | Out-Null
    $ready=Join-Path $out 'game.ready';if(Test-Path $ready){Remove-Item $ready}
    $game=Start-Process ([IO.Path]::GetFullPath("$PSScriptRoot/results-review3/probe/SwapchainProbe.exe")) -ArgumentList "--api 12 --borderless --seconds 20 --ready `"$ready`"" -PassThru
    $deadline=[DateTime]::UtcNow.AddSeconds(5); do { Start-Sleep -Milliseconds 100 } while((Invoke-ProbeCore $session getSnapshot).runtime.activeGameProfileId -ne 'live' -and [DateTime]::UtcNow -lt $deadline)
    $result.ActiveGame=(Invoke-ProbeCore $session getSnapshot).runtime.activeGameProfileId
    Start-Sleep -Milliseconds 500; $before=Red (Read-Colors game-before)
    Invoke-ProbeCore $session saveGameProfile @{id='live';name='Live';processName='swapchainprobe.exe';crosshairId=$id;displayColorsEnabled=$true;displayColors=@{gamma=130;contrast=110;brightness=100;vibrance=70}} | Out-Null
    Start-Sleep -Milliseconds 500; $saved=Red (Read-Colors game-saved)
    Invoke-ProbeCore $session previewDisplayColors @{colors=@{gamma=130;contrast=110;brightness=100;vibrance=70}} | Out-Null; Start-Sleep -Milliseconds 300; Read-Colors game-reference | Out-Null
    Invoke-ProbeCore $session previewDisplayColors @{colors=$null} | Out-Null
    $result.GameRedBeforeSave=$before[0]; $result.GameRedAfterSave=$saved[0]
    $result.SavedGameColorsAppliedImmediately=(Same game-saved game-reference) -and ($saved[0] -ne $before[0])
    $result.Error=(Invoke-ProbeCore $session getSnapshot).runtime.displayColorsError
} finally {
    if($game){if(!$game.HasExited){$game.Kill()};$game.Dispose()}
    Stop-ProbeCore $session
    Read-Colors exited | Out-Null
    $result.OriginalRestoredExactly=(Same original exited) -and !(Test-Path (Join-Path $root 'v2/colors-applied.json'))
}
$result.Executable=$Executable
$result | ConvertTo-Json | Tee-Object (Join-Path $out 'summary.json')
if(!$result.OriginalRestoredExactly){throw 'ORIGINAL COLORS NOT RESTORED'}
