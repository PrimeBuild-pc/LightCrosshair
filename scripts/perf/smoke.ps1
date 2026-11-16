param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
$ErrorActionPreference = 'Stop'
if (Get-Process LightCrosshair -ErrorAction SilentlyContinue) { throw 'Close the existing Core before smoke testing' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('lc2-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $testRoot 'v2'
$app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
$env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot
$script:requestId = 0
$script:pipe = $null
$script:reader = $null
$events = [Collections.Generic.List[string]]::new()
function Connect-Core {
    $script:pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$([Diagnostics.Process]::GetCurrentProcess().SessionId)", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $script:pipe.Connect(5000)
    $script:reader = [IO.StreamReader]::new($script:pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
    Invoke-Core hello @{ protocolVersion = 1; client = 'local-smoke' } | Out-Null
}
function Invoke-Core([string]$Method, $Payload = $null) {
    $script:requestId++
    $request = @{ kind = 'req'; id = $script:requestId; method = $Method; payload = $Payload } | ConvertTo-Json -Compress -Depth 50
    $bytes = [Text.Encoding]::UTF8.GetBytes($request + "`n")
    $script:pipe.Write($bytes, 0, $bytes.Length); $script:pipe.Flush()
    do {
        $line = $script:reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult()
        if (!$line) { throw 'Core disconnected' }
        $response = $line | ConvertFrom-Json -Depth 50
        if ($response.kind -eq 'evt') { $events.Add($response.method) }
    } until ($response.kind -eq 'res' -and $response.id -eq $script:requestId)
    if (!$response.ok) { throw "Core returned $($response.error.code) for $Method" }
    return $response.payload
}
try {
    Connect-Core
    $snapshot = Invoke-Core getSnapshot
    if (!$snapshot.store.settings.defaultCrosshairId) { throw 'Missing default crosshair' }
    $presets = Invoke-Core getPresets
    if ($presets.Count -ne 30) { throw 'Preset count mismatch' }
    $design = $presets[0].design
    $design.id = [Guid]::NewGuid().ToString('N'); $design.name = 'Smoke crosshair'
    $saved = Invoke-Core saveCrosshair $design
    if ($saved.name -ne 'Smoke crosshair') { throw 'Save failed' }
    $snapshot = Invoke-Core getSnapshot
    $settings = $snapshot.store.settings; $settings.crosshairVisible = $false
    Invoke-Core saveSettings $settings | Out-Null
    Invoke-Core preview @{ design = $saved } | Out-Null
    $snapshot = Invoke-Core getSnapshot
    if (!$snapshot.runtime.previewActive -or !$snapshot.runtime.crosshairShown) { throw 'Preview did not override hidden visibility' }
    $renderTimes = @(); $previewTimes = @()
    for ($i = 0; $i -lt 50; $i++) {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $png = Invoke-Core render @{ design = $saved; canvasSize = 128 }
        $renderTimes += $watch.Elapsed.TotalMilliseconds
        $saved.layers[0].offsetX = $i % 3
        $watch.Restart(); Invoke-Core preview @{ design = $saved } | Out-Null
        $previewTimes += $watch.Elapsed.TotalMilliseconds
    }
    $imagePath = Join-Path $testRoot 'source.png'
    [IO.File]::WriteAllBytes($imagePath, [Convert]::FromBase64String($png.pngBase64))
    $image = Invoke-Core importImage @{ value = $imagePath }
    if ($image.value -notmatch '^[a-f0-9]{32}\.png$') { throw 'Image import failed' }
    $code = Invoke-Core encodeShareCode $saved
    $decoded = Invoke-Core decodeShareCode @{ value = $code.value }
    if ($decoded.id -eq $saved.id) { throw 'Share code reused identity' }
    $exportPath = Join-Path $testRoot 'smoke.lcx.json'
    Invoke-Core exportCrosshairs @{ path = $exportPath; crosshairIds = @($saved.id) } | Out-Null
    $imported = Invoke-Core importCrosshairs @{ value = $exportPath }
    if ($imported.imported.Count -ne 1) { throw 'JSON import failed' }
    $game = Invoke-Core saveGameProfile @{ name = 'Smoke game'; processName = 'lc2-smoke-game.exe'; crosshairId = $saved.id }
    Invoke-Core deleteGameProfile @{ id = $game.id } | Out-Null
    Invoke-Core deleteCrosshair @{ id = $saved.id } | Out-Null
    $script:reader.Dispose(); $script:pipe.Dispose()
    Connect-Core
    $snapshot = Invoke-Core getSnapshot
    if ($snapshot.runtime.previewActive) { throw 'Disconnect did not end preview' }
    $renderSorted = @($renderTimes | Sort-Object); $previewSorted = @($previewTimes | Sort-Object)
    $result = [ordered]@{
        Passed = $true; CorePid = $app.Id; TemporaryData = $testRoot
        RenderRoundTripMedianMs = [math]::Round($renderSorted[25], 3)
        RenderRoundTripP95Ms = [math]::Round($renderSorted[47], 3)
        PreviewRoundTripMedianMs = [math]::Round($previewSorted[25], 3)
        PreviewRoundTripP95Ms = [math]::Round($previewSorted[47], 3)
        SnapshotEvents = @($events | Where-Object { $_ -eq 'snapshotChanged' }).Count
        RuntimeEvents = @($events | Where-Object { $_ -eq 'runtimeChanged' }).Count
    }
    $result | ConvertTo-Json | Set-Content (Join-Path $testRoot 'summary.json')
    $result | ConvertTo-Json
}
finally {
    if ($script:pipe -and $script:pipe.IsConnected) {
        try { Invoke-Core quit | Out-Null } catch { }
    }
    if ($script:reader) { $script:reader.Dispose() }
    if ($script:pipe) { $script:pipe.Dispose() }
    if (!$app.WaitForExit(10000)) { $app.Kill() }
    $app.Dispose()
}
