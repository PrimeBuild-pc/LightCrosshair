param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-cycle-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$nativeSource = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
Add-Type -TypeDefinition (($nativeSource -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT; $previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
$pipe = $null; $reader = $null; $app = $null
try {
    $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $root 'v2'
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
    $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $pipe.Connect(10000)
    $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
    $script:cycleId = 0; $script:snapshots = 0
    function Invoke-Cycle([string]$method, $payload = $null) {
        $script:cycleId++
        $bytes = [Text.Encoding]::UTF8.GetBytes((@{kind='req';id=$script:cycleId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress) + "`n")
        $pipe.Write($bytes, 0, $bytes.Length); $pipe.Flush()
        do {
            $response = $reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32
            if ($response.kind -eq 'evt' -and $response.method -eq 'snapshotChanged') { $script:snapshots++ }
        } until ($response.kind -eq 'res' -and $response.id -eq $script:cycleId)
        if (!$response.ok) { throw $response.error.code }
        return $response.payload
    }
    Invoke-Cycle hello @{protocolVersion=1;client='cycle-smoke'} | Out-Null
    $snapshot = Invoke-Cycle getSnapshot
    $original = $snapshot.store.crosshairs[0]
    $original.favorite = $true
    Invoke-Cycle saveCrosshair $original | Out-Null
    $extra = $original.PSObject.Copy(); $extra.id = [Guid]::NewGuid().ToString('N'); $extra.name = 'Cycle alternate'; $extra.favorite = $false
    Invoke-Cycle saveCrosshair $extra | Out-Null
    $window = [PerfNative]::Find($app.Id, $false)
    $before = $script:snapshots
    [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]9001, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 700
    $after = Invoke-Cycle getSnapshot
    if ($after.runtime.activeCrosshairId -ne $extra.id) { throw 'One favorite must cycle through all user crosshairs' }
    if ($after.store.settings.defaultCrosshairId -ne $extra.id) { throw 'Cycle did not persist the default' }
    if ($script:snapshots -ne $before + 1) { throw 'Cycle must emit one snapshotChanged' }
    [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]9002, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 700
    $after = Invoke-Cycle getSnapshot
    if ($after.store.settings.defaultCrosshairId -ne $original.id) { throw 'Previous did not persist the original default' }
    Invoke-Cycle quit | Out-Null
    if (!$app.WaitForExit(10000)) { throw 'Core did not exit' }
    $saved = Get-Content -LiteralPath (Join-Path $env:LIGHTCROSSHAIR_DATA_ROOT 'store.json') -Raw | ConvertFrom-Json -Depth 32
    if ($saved.settings.defaultCrosshairId -ne $original.id) { throw 'Cycle did not survive shutdown' }
    Write-Host "PASS: one-favorite fallback, next/previous, persistence and snapshotChanged ($root)"
} finally {
    if ($reader) { $reader.Dispose() }; if ($pipe) { $pipe.Dispose() }
    if ($app) { if (!$app.HasExited) { $app.Kill() }; $app.Dispose() }
    $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot; $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance
}
