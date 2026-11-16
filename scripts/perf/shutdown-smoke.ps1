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
        $locked = $null
        if ($brokenMigration) {
            [IO.File]::WriteAllText((Join-Path $root 'profiles.json'), '[]')
            $locked = [IO.File]::Open((Join-Path $root 'profiles.json'), [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        }
        $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
        $pipe = $null; $reader = $null
        try {
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
            if ($brokenMigration) {
                if ($app.HasExited -or $snapshot.store.crosshairs.Count -ne 1) { throw 'Load failure did not keep Core running with defaults' }
                $design = $snapshot.store.crosshairs[0]; $design.name = 'Temporary edit'
                Send-Case saveCrosshair $design; Read-Case | Out-Null
                Send-Case getSnapshot $null; $temporary = Read-Case
                if ($temporary.store.crosshairs[0].name -ne 'Temporary edit') { throw 'Temporary library is not usable' }
                Send-Case quit $null; Read-Case | Out-Null
                if (!$app.WaitForExit(10000)) { throw 'Fallback Core did not exit' }
                if (Test-Path -LiteralPath (Join-Path $root 'v2/store.json')) { throw 'Failed load wrote a default store marker' }
                $log = [IO.File]::ReadAllText((Join-Path $root 'v2/logs/core.log'))
                if ([regex]::Matches($log, 'Load failure notification:').Count -ne 1) { throw 'Expected exactly one load-failure notification' }
                if ($log -notmatch 'System.IO.IOException:' -or $log -notmatch 'at LightCrosshair.ProfileStore.LoadAsync') { throw 'Missing exception message or stack' }
                $locked.Dispose(); $locked = $null
                if ([IO.File]::ReadAllText((Join-Path $root 'profiles.json')) -ne '[]') { throw 'Migration changed v1 data' }
                Write-Host "PASS: published Core survives an unreadable legacy file, supports temporary edits, notifies once, logs details and writes no store ($root)"
                continue
            }
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
            if ($locked) { $locked.Dispose() }
            if ($reader) { $reader.Dispose() }
            if ($pipe) { $pipe.Dispose() }
            if (!$app.HasExited) { $app.Kill() }
            $app.Dispose()
        }
    }
}
finally { $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot; $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance }
