# Open item 1: a real Alt+F press toggles the FPS overlay setting (persisted) and the overlay window.
param([string]$Executable="$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
. "$PSScriptRoot/ReviewProbe.ps1"
Add-Type @'
using System.Runtime.InteropServices;
public static class FpsKeys {
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
    public static void AltF() { keybd_event(0x12,0,0,System.UIntPtr.Zero); keybd_event(0x46,0,0,System.UIntPtr.Zero); keybd_event(0x46,0,2,System.UIntPtr.Zero); keybd_event(0x12,0,2,System.UIntPtr.Zero); }
}
'@
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc-fps-hotkey-'+[guid]::NewGuid().ToString('N'))
$session=$null
function Enabled { (Invoke-ProbeCore $session getSnapshot).store.settings.fpsOverlay.enabled }
try {
    $session=Start-ProbeCore $Executable $root
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='fps-hotkey'} | Out-Null
    $snapshot=Invoke-ProbeCore $session getSnapshot
    if($snapshot.runtime.hotkeyErrors | Where-Object action -eq 'ToggleFpsOverlay'){throw "Alt+F could not be registered: $($snapshot.runtime.hotkeyErrors | ConvertTo-Json -Compress)"}
    $states=@(Enabled)
    foreach($n in 1..2){ [FpsKeys]::AltF(); Start-Sleep -Milliseconds 600; $states+=Enabled }
    if($states[0] -or !$states[1] -or $states[2]){throw "FPS overlay did not toggle on and off: $($states -join ',')"}
    "PASS: Alt+F toggled the FPS overlay setting $($states -join ' -> ')"
} finally { Stop-ProbeCore $session }
