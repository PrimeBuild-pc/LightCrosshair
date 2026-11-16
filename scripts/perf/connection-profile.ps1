param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [Parameter(Mandatory)][string]$DiagnosticsTools,
    [string]$OutputDirectory = "$PSScriptRoot/results-connections"
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-connections-' + [Guid]::NewGuid().ToString('N'))
$app = $null; $counter = $null; $pipe = $null; $reader = $null
try {
    $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $root 'v2'
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
    $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
    $counter = Start-Process -FilePath (Join-Path $DiagnosticsTools 'dotnet-counters.exe') -ArgumentList @('collect', '--process-id', $app.Id, '--counters', 'System.Runtime', '--refresh-interval', '1', '--format', 'csv', '--output', "$OutputDirectory/runtime.csv", '--duration', '00:03:00') -PassThru -WindowStyle Hidden
    $script:connectionId = 0
    function Connect-Profile {
        $script:pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        $script:pipe.Connect(10000)
        $script:reader = [IO.StreamReader]::new($script:pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
        Invoke-Profile hello @{protocolVersion=1;client='connection-profile'} | Out-Null
    }
    function Invoke-Profile([string]$method, $payload = $null) {
        $script:connectionId++
        $bytes = [Text.Encoding]::UTF8.GetBytes((@{kind='req';id=$script:connectionId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress) + "`n")
        $script:pipe.Write($bytes, 0, $bytes.Length); $script:pipe.Flush()
        do { $response = $script:reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32 }
        until ($response.kind -eq 'res' -and $response.id -eq $script:connectionId)
        if (!$response.ok) { throw $response.error.code }
        return $response.payload
    }
    for ($cycle = 1; $cycle -le 20; $cycle++) {
        Connect-Profile
        Invoke-Profile getSnapshot | Out-Null
        $presets = Invoke-Profile getPresets
        foreach ($preset in $presets) { Invoke-Profile render @{design=$preset.design;canvasSize=64} | Out-Null }
        $script:reader.Dispose(); $script:pipe.Dispose()
        Start-Sleep -Milliseconds 200
        if ($cycle -in @(5,20)) {
            & (Join-Path $DiagnosticsTools 'dotnet-gcdump.exe') collect --process-id $app.Id --output "$OutputDirectory/cycle-$cycle.gcdump"
            if ($LASTEXITCODE) { throw 'Heap collection failed' }
            & (Join-Path $DiagnosticsTools 'dotnet-gcdump.exe') report "$OutputDirectory/cycle-$cycle.gcdump" | Set-Content "$OutputDirectory/heap-$cycle.txt"
            if ($LASTEXITCODE) { throw 'Heap report failed' }
        }
    }
    Connect-Profile
    Invoke-Profile quit | Out-Null
    if (!$app.WaitForExit(10000)) { throw 'Core did not exit' }
    Write-Host "PASS: 20 Core-only connections and 600 thumbnail renders; heap reports/counters in $OutputDirectory. This does not measure real UI lifecycle."
}
finally {
    if ($script:reader) { $script:reader.Dispose() }
    if ($script:pipe) { $script:pipe.Dispose() }
    if ($app) { if (!$app.HasExited) { $app.Kill() }; $app.Dispose() }
    if ($counter) { if (!$counter.HasExited) { $counter.Kill() }; $counter.Dispose() }
    $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance
}
