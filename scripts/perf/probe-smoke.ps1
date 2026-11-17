param([string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe")
$ErrorActionPreference = 'Stop'
if (Get-Process LightCrosshair -ErrorAction SilentlyContinue) { throw 'Close existing Core instances before checking normal/test probes' }
$source = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
$native = (($source -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
Add-Type -TypeDefinition $native
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
try {
    foreach ($test in @($false, $true)) {
        $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path ([IO.Path]::GetTempPath()) ('lc2-probes-' + [Guid]::NewGuid().ToString('N') + '/v2')
        $env:LIGHTCROSSHAIR_TEST_INSTANCE = if ($test) { [Guid]::NewGuid().ToString('N') } else { $null }
        $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
        $window = [IntPtr]::Zero
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            do { $window = [PerfNative]::Find($app.Id, $false); Start-Sleep -Milliseconds 50 }
            until ($window -ne [IntPtr]::Zero -or $app.HasExited -or [DateTime]::UtcNow -gt $deadline)
            if ($window -eq [IntPtr]::Zero) { throw 'Core window missing' }
            $magic = [PerfNative]::Query($window, 0x8069)
            if ($test -and $magic -ne 0x4C433230) { throw 'Test probes missing' }
            if (!$test) {
                foreach ($message in @(0x8064, 0x8065, 0x8066, 0x8067, 0x8069)) {
                    if ([PerfNative]::Query($window, $message) -ne 0) { throw "Production probe $message is enabled" }
                }
            }
            Write-Host "PASS: diagnostic probes enabled=$test"
        }
        finally {
            if (!$app.HasExited) {
                [void][PerfNative]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
                if (!$app.WaitForExit(5000)) { $app.Kill() }
            }
            $app.Dispose()
        }
    }
}
finally { $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot; $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance }
