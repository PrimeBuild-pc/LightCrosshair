param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [ValidateSet('v1.9-working', 'v1.9-disabled-working', 'v1.9-enabled-working')][string]$Fixture = 'v1.9-working'
)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-migration-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
Copy-Item -Path "$PSScriptRoot/../../LightCrosshair.Tests/Fixtures/$Fixture/*" -Destination $root
$nativeSource = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
$native = (($nativeSource -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
Add-Type -TypeDefinition $native
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MigrationNative {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
}
'@
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
$pipe = $null; $reader = $null; $app = $null
try {
    $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $root 'v2'
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
    $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $pipe.Connect(10000)
    $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
    $script:migrationId = 0
    function Invoke-Migration([string]$method, $payload = $null) {
        $script:migrationId++
        $json = @{kind='req';id=$script:migrationId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($json + "`n")
        $pipe.Write($bytes, 0, $bytes.Length); $pipe.Flush()
        do { $response = $reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32 }
        until ($response.kind -eq 'res' -and $response.id -eq $script:migrationId)
        if (!$response.ok) { throw $response.error.code }
        return $response.payload
    }
    Invoke-Migration hello @{protocolVersion=1;client='migration-smoke'} | Out-Null
    $snapshot = Invoke-Migration getSnapshot
    $expectedCount = if ($Fixture -eq 'v1.9-working') { 1 } else { 2 }
    if ($app.HasExited -or $snapshot.store.crosshairs.Count -ne $expectedCount -or
        $snapshot.store.crosshairs[0].id -ne 'default-immutable' -or
        $snapshot.store.crosshairs[0].name -ne 'Default edited' -or
        $snapshot.store.settings.defaultCrosshairId -ne 'default-immutable') { throw 'v1.9 working copy migration failed' }
    if ($Fixture -ne 'v1.9-working') {
        $expectedVisible = $Fixture -eq 'v1.9-enabled-working'
        foreach ($design in $snapshot.store.crosshairs) {
            if (@($design.layers | Where-Object { !$_.visible }).Count) { throw 'Legacy master switch hid a layer' }
        }
        $window = [PerfNative]::Find($app.Id, $false)
        if ($snapshot.store.settings.crosshairVisible -ne $expectedVisible -or $snapshot.runtime.crosshairShown -ne $expectedVisible -or [MigrationNative]::IsWindowVisible($window) -ne $expectedVisible) { throw 'Initial migrated visibility is incorrect' }
        # The same settings mutation used by the UI's Show crosshair switch.
        $settings = $snapshot.store.settings; $settings.crosshairVisible = $true
        Invoke-Migration saveSettings $settings | Out-Null
        $shown = Invoke-Migration getSnapshot
        if (!$shown.runtime.crosshairShown -or ![MigrationNative]::IsWindowVisible($window) -or [PerfNative]::Query($window, 0x8064) -lt 1) { throw 'Show crosshair did not present a visible overlay' }
        Write-Host "PASS: $Fixture keeps all layers visible and migrates the active master flag globally; Show crosshair presents the overlay"
    }
    Invoke-Migration quit | Out-Null
    if (!$app.WaitForExit(10000)) { throw 'Core did not exit' }
    Write-Host "PASS: published Core starts with the real v1.9 working-copy shape, merges its source and resolves the default ($root)"
}
finally {
    if ($reader) { $reader.Dispose() }
    if ($pipe) { $pipe.Dispose() }
    if ($app) { if (!$app.HasExited) { $app.Kill() }; $app.Dispose() }
    $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance
}
