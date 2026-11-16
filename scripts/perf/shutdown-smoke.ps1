param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
$native = (($source -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
Add-Type -TypeDefinition $native
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
try {
    foreach ($brokenMigration in @($true, $false)) {
        $root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-shutdown-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root | Out-Null
        $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $root 'v2'
        $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
        if ($brokenMigration) { [IO.File]::WriteAllText((Join-Path $root 'profiles.json'), '[{"Id":"bad/id","Name":"Invalid identity"}]') }
        $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
        $pipe = $null; $reader = $null
        try {
            if ($brokenMigration) {
                if (!$app.WaitForExit(10000)) { throw 'Broken migration did not stop cleanly' }
                if (Test-Path -LiteralPath (Join-Path $root 'v2/store.json')) { throw 'Failed migration wrote a default store marker' }
                if ([IO.File]::ReadAllText((Join-Path $root 'profiles.json')) -ne '[{"Id":"bad/id","Name":"Invalid identity"}]') { throw 'Migration changed v1 data' }
                Write-Host 'PASS: failed migration preserves v1 and does not create store.json'
                continue
            }
            $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "LightCrosshair.v2.$($app.SessionId).test.$env:LIGHTCROSSHAIR_TEST_INSTANCE", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
            $pipe.Connect(5000)
            $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 4096, $true)
            $script:caseId = 0
            function Send-Case([string]$method, $payload) {
                $script:caseId++
                $json = @{kind='req';id=$script:caseId;method=$method;payload=$payload} | ConvertTo-Json -Depth 32 -Compress
                $bytes = [Text.Encoding]::UTF8.GetBytes($json + "`n")
                $pipe.Write($bytes, 0, $bytes.Length); $pipe.Flush()
            }
            function Read-Case {
                do { $response = $reader.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult() | ConvertFrom-Json -Depth 32 }
                until ($response.kind -eq 'res' -and $response.id -eq $script:caseId)
                if (!$response.ok) { throw $response.error.code }
                return $response.payload
            }
            Send-Case hello @{protocolVersion=1;client='shutdown-smoke'}; Read-Case | Out-Null
            Send-Case getSnapshot $null; $snapshot = Read-Case
            $design = $snapshot.store.crosshairs[0]; $design.name = 'Shutdown race'
            Send-Case saveCrosshair $design
            # Close during the 300 ms persistence debounce, without waiting for the response.
            Start-Sleep -Milliseconds 100
            $window = [PerfNative]::Find($app.Id, $false)
            [void][PerfNative]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
            if (!$app.WaitForExit(10000)) { throw 'Concurrent save/shutdown did not complete' }
            $stored = Get-Content -LiteralPath (Join-Path $root 'v2/store.json') -Raw | ConvertFrom-Json -Depth 32
            if ($stored.crosshairs[0].name -ne 'Shutdown race') { throw 'Shutdown lost the in-flight save' }
            Write-Host 'PASS: shutdown preserves a save already in flight'
        }
        finally {
            if ($reader) { $reader.Dispose() }
            if ($pipe) { $pipe.Dispose() }
            if (!$app.HasExited) { $app.Kill() }
            $app.Dispose()
        }
    }
}
finally { $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot; $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance }
