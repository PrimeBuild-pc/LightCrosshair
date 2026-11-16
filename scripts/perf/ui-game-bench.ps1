param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [string]$Probe = "$PSScriptRoot/results-review3/probe/SwapchainProbe.exe",
    [string]$OutputDirectory = "$PSScriptRoot/results-review3/ui-after",
    [string]$Stage = 'after',
    [int]$Cycles = 5
)
. "$PSScriptRoot/ReviewProbe.ps1"
. "$PSScriptRoot/ProfileGuard.ps1"
$out = [IO.Path]::GetFullPath($OutputDirectory); New-Item -ItemType Directory -Path $out -Force | Out-Null
$guard = Start-RealProfileGuard (Join-Path $out 'profile-backup')
$session=$null; $probeProcess=$null; $pipeReleased=$false
try {
    dotnet build "$PSScriptRoot/UiOpenProbe" -c Release --nologo
    if ($LASTEXITCODE) { throw 'UI observer build failed' }
    $root=Join-Path ([IO.Path]::GetTempPath()) ('lc3-ui-'+[guid]::NewGuid().ToString('N'))
    $session=Start-ProbeCore $Executable $root -ProductionPipe
    Invoke-ProbeCore $session hello @{protocolVersion=1;client='ui-bench-setup'} | Out-Null
    $snapshot=Invoke-ProbeCore $session getSnapshot
    $snapshot.store.settings.fpsOverlay.enabled=$false
    Invoke-ProbeCore $session saveSettings $snapshot.store.settings | Out-Null
    $session.Reader.Dispose(); $session.Pipe.Dispose(); $pipeReleased=$true
    $ready=Join-Path $out 'probe.ready'; if(Test-Path -LiteralPath $ready){Remove-Item -LiteralPath $ready}
    $frames=Join-Path $out 'frames.csv'; $seconds=15+$Cycles*4
    $probeProcess=Start-Process -FilePath ([IO.Path]::GetFullPath($Probe)) -ArgumentList "--api 12 --borderless --seconds $seconds --output `"$frames`" --ready `"$ready`"" -PassThru -WindowStyle Normal
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while(!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 25}
    if(!(Test-Path -LiteralPath $ready)){throw 'DX12 probe did not initialize'}
    $handle=(Get-Content -LiteralPath $ready).Split(',')[1]
    Start-Sleep -Seconds 2
    & "$PSScriptRoot/UiOpenProbe/bin/Release/net10.0-windows/UiOpenProbe.exe" $session.App.Id $handle $Cycles (Join-Path $out 'ui.csv') $Stage
    if($LASTEXITCODE){throw 'UI observer failed'}
    [void]$probeProcess.CloseMainWindow()
    if(!$probeProcess.WaitForExit(5000)){throw 'DX12 probe did not exit'}
    $probeProcess.Dispose(); $probeProcess=$null
    $intervals=@((Import-Csv $frames | Select-Object -Skip 360).IntervalMs | ForEach-Object {[double]$_} | Sort-Object)
    [pscustomobject]@{Stage=$Stage;Samples=$intervals.Count;MeanMs=($intervals|Measure-Object -Average).Average;P95Ms=$intervals[[int]($intervals.Count*.95)];P99Ms=$intervals[[int]($intervals.Count*.99)];MaxMs=$intervals[-1];DataRoot=$root} | ConvertTo-Json | Set-Content (Join-Path $out 'summary.json')
    Get-Content (Join-Path $out 'summary.json')
} finally {
    if($probeProcess){if(!$probeProcess.HasExited){$probeProcess.Kill()};$probeProcess.Dispose()}
    if($session -and $pipeReleased -and !$session.App.HasExited){
        $session.Pipe=[IO.Pipes.NamedPipeClientStream]::new('.', $session.PipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        $session.Pipe.Connect(5000);$session.Reader=[IO.StreamReader]::new($session.Pipe,[Text.Encoding]::UTF8,$false,4096,$true)
        Invoke-ProbeCore $session hello @{protocolVersion=1;client='ui-bench-shutdown'} | Out-Null
    }
    try { Stop-ProbeCore $session } finally { Restore-RealProfileGuard $guard (Join-Path $out 'profile-audit.json') }
}
