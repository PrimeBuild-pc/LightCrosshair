param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
. "$PSScriptRoot/ReviewProbe.ps1"
. "$PSScriptRoot/ProfileGuard.ps1"
if(Get-Process 'LightCrosshair.UI' -ErrorAction SilentlyContinue){throw 'Close other UI instances before this test'}
$root=Join-Path ([IO.Path]::GetTempPath()) ('lc3-park-'+[guid]::NewGuid().ToString('N'))
$guard=Start-RealProfileGuard (Join-Path $root 'profile-backup')
$session=$null;$ui=$null;$released=$false
try {
    $session=Start-ProbeCore $Executable $root -ProductionPipe
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='park-setup'} | Out-Null
    $window=[PerfNative]::Find($session.App.Id,$false)
    $session.Reader.Dispose();$session.Pipe.Dispose();$released=$true
    [void][PerfNative]::PostMessage($window,0x312,[IntPtr]9003,[IntPtr]::Zero)
    $deadline=[DateTime]::UtcNow.AddSeconds(8)
    do {
        Start-Sleep -Milliseconds 50
        $ui=Get-Process 'LightCrosshair.UI' -ErrorAction SilentlyContinue | Select-Object -First 1
        if($ui){$ui.Refresh()}
    } while((!$ui -or $ui.MainWindowHandle -eq [IntPtr]::Zero) -and [DateTime]::UtcNow -lt $deadline)
    if(!$ui -or !$ui.MainWindowHandle){throw 'UI did not open'}
    Start-Sleep -Seconds 1
    $handle=$ui.MainWindowHandle
    [void][ReviewNative]::ShowWindow($handle,0)
    if([ReviewNative]::IsWindowVisible($handle)){throw 'UI did not hide'}
    [void][PerfNative]::PostMessage($window,0x312,[IntPtr]9003,[IntPtr]::Zero)
    Start-Sleep -Seconds 1
    if($ui.HasExited){throw 'OpenSettings closed the parked UI instead of requesting focus'}
    $others=Get-Process 'LightCrosshair.UI' | Where-Object Id -ne $ui.Id
    if($others){throw 'OpenSettings spawned another UI process'}
    Write-Host 'PASS: a hidden, connected UI receives focus without closing or spawning a replacement'
} finally {
    try {
    if($ui){if(!$ui.HasExited){[void][ReviewNative]::ShowWindow($ui.MainWindowHandle,5);[void]$ui.CloseMainWindow();if(!$ui.WaitForExit(5000)){$ui.Kill()}};$ui.Dispose()}
    if($session -and $released -and !$session.App.HasExited){
        $session.Pipe=[IO.Pipes.NamedPipeClientStream]::new('.', $session.PipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        $session.Pipe.Connect(5000);$session.Reader=[IO.StreamReader]::new($session.Pipe,[Text.Encoding]::UTF8,$false,4096,$true)
        Invoke-ProbeCore $session hello @{protocolVersion=1;client='park-shutdown'} | Out-Null
    }
        Stop-ProbeCore $session
    } finally { Restore-RealProfileGuard $guard (Join-Path $root 'profile-audit.json') }
}
