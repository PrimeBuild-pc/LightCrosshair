param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-capture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$nativeSource = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
$native = (($nativeSource -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
Add-Type -TypeDefinition $native
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
    $script:captureId = 0; $script:closeEvents = 0
    function Invoke-Capture([string]$method, $payload = $null) {
        $script:captureId++
        $json = @{kind='req';id=$script:captureId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($json + "`n")
        $pipe.Write($bytes, 0, $bytes.Length); $pipe.Flush()
        do {
            $response = $reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32
            if ($response.kind -eq 'evt' -and $response.method -eq 'closeRequested') { $script:closeEvents++ }
        }
        until ($response.kind -eq 'res' -and $response.id -eq $script:captureId)
        if (!$response.ok) { throw $response.error.code }
        return $response.payload
    }
    Invoke-Capture hello @{protocolVersion=1;client='capture-smoke'} | Out-Null
    $window = [PerfNative]::Find($app.Id, $false)
    function Post-Action([int]$id) {
        [void][PerfNative]::PostMessage($window, 0x312, [IntPtr]$id, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 600
        return Invoke-Capture getSnapshot
    }
    $snapshot = Invoke-Capture getSnapshot
    $initialVisible = $snapshot.store.settings.crosshairVisible
    $initialId = $snapshot.runtime.activeCrosshairId
    $extra = $snapshot.store.crosshairs[0].PSObject.Copy()
    $extra.id = [Guid]::NewGuid().ToString('N'); $extra.name = 'Capture alternate'
    Invoke-Capture saveCrosshair $extra | Out-Null
    Invoke-Capture setInputCapture @{active=$true} | Out-Null
    foreach ($id in 9000..9003) {
        $during = Post-Action $id
        if ($during.store.settings.crosshairVisible -ne $initialVisible -or $during.runtime.activeCrosshairId -ne $initialId -or $script:closeEvents) { throw "Action $id fired during capture" }
    }
    Invoke-Capture setInputCapture @{active=$false} | Out-Null
    if ((Post-Action 9000).store.settings.crosshairVisible -eq $initialVisible) { throw 'Explicit end did not restore actions' }
    Invoke-Capture setInputCapture @{active=$true} | Out-Null
    $reader.Dispose(); $pipe.Dispose()
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $pipe.Connect(5000); $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
    Invoke-Capture hello @{protocolVersion=1;client='capture-reconnect'} | Out-Null
    if ((Post-Action 9000).store.settings.crosshairVisible -ne $initialVisible) { throw 'Disconnect did not restore actions' }
    Invoke-Capture setInputCapture @{active=$true} | Out-Null
    Start-Sleep -Seconds 30
    Invoke-Capture setInputCapture @{active=$true} | Out-Null
    Start-Sleep -Seconds 32
    if ((Post-Action 9000).store.settings.crosshairVisible -ne $initialVisible) { throw 'Renewed capture expired too early' }
    Start-Sleep -Seconds 30
    if ((Post-Action 9000).store.settings.crosshairVisible -eq $initialVisible) { throw '60-second expiry did not restore actions' }
    Invoke-Capture quit | Out-Null
    if (!$app.WaitForExit(10000)) { throw 'Core did not exit' }
    Write-Host "PASS: all actions suppressed during capture; explicit end, disconnect, renewal and 60-second expiry verified ($root)"
}
finally {
    if ($reader) { $reader.Dispose() }
    if ($pipe) { $pipe.Dispose() }
    if ($app) { if (!$app.HasExited) { $app.Kill() }; $app.Dispose() }
    $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance
}

