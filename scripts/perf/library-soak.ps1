param(
    [string]$Executable = "$PSScriptRoot/../../releases/LightCrosshair-v2.0.0-win-x64/LightCrosshair.exe",
    [ValidateRange(1, 200)][int]$Sessions = 50,
    [string]$OutputDirectory = "$PSScriptRoot/results-library-soak",
    [switch]$AllowUiPlacementBackup,
    [string]$DiagnosticsTools
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/ProfileGuard.ps1"
if (Get-Process LightCrosshair,LightCrosshair.UI -ErrorAction SilentlyContinue) { throw 'Close existing Core/UI processes before the Library soak' }
$uiPath = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($Executable))) 'LightCrosshair.UI.exe'
if (!(Test-Path -LiteralPath $uiPath)) { throw 'Publish Core and UI into the same folder first' }
$placementSource = Get-Content -LiteralPath "$PSScriptRoot/../../LightCrosshair.UI/Services/WindowPlacementStore.cs" -Raw
if ($placementSource.Contains('SpecialFolder.ApplicationData') -and !$placementSource.Contains('LIGHTCROSSHAIR_DATA_ROOT') -and !$AllowUiPlacementBackup) {
    throw 'UI placement is not isolated. Supply -AllowUiPlacementBackup only when the placement-only backup/restore exception is authorized.'
}
$nativeSource = Get-Content -LiteralPath "$PSScriptRoot/measure.ps1" -Raw
$native = (($nativeSource -split "Add-Type @'", 2)[1] -split "'@", 2)[0]
Add-Type -TypeDefinition $native
Add-Type -Path "$PSScriptRoot/LibrarySoakRelay.cs"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-library-soak-' + [Guid]::NewGuid().ToString('N'))
$previousRoot = $env:LIGHTCROSSHAIR_DATA_ROOT
$previousInstance = $env:LIGHTCROSSHAIR_TEST_INSTANCE
$app = $null; $ui = $null; $relay = $null; $guard = $null; $summary = $null
$curve = [Collections.Generic.List[object]]::new()
try {
    $guard = Start-RealProfileGuard (Join-Path $root 'profile-backup')
    $env:LIGHTCROSSHAIR_DATA_ROOT = Join-Path $root 'v2'
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = [Guid]::NewGuid().ToString('N')
    $app = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -PassThru -WindowStyle Hidden
    $publicPipe = "LightCrosshair.v2.$($app.SessionId)"
    $corePipe = "$publicPipe.test.$env:LIGHTCROSSHAIR_TEST_INSTANCE"
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { $window = [PerfNative]::Find($app.Id, $false); Start-Sleep -Milliseconds 50 }
    until ($window -ne [IntPtr]::Zero -or $app.HasExited -or [DateTime]::UtcNow -gt $deadline)
    if ($window -eq [IntPtr]::Zero) { throw 'Core window missing' }
    Start-Sleep -Seconds 10
    $app.Refresh(); $before = $app.PrivateMemorySize64
    for ($cycle = 1; $cycle -le $Sessions; $cycle++) {
        $relay = [LibrarySoakRelay]::new($publicPipe, $corePipe)
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $ui = Start-Process -FilePath $uiPath -ArgumentList @('--page', 'library') -PassThru -WindowStyle Hidden
        $scrolled = $false
        while (!$relay.Ready) {
            if ($relay.Failure) { throw $relay.Failure }
            if ($ui.HasExited -or $watch.Elapsed.TotalSeconds -gt 20) { throw "Library session $cycle failed: $($relay.Rendered)/$($relay.Expected) thumbnails" }
            if (!$scrolled -and $relay.Rendered -ge 19) {
                $found = agent-desktop find --app LightCrosshair.UI.exe --native-id PresetList --timeout-ms 5000 | ConvertFrom-Json
                if (!$found.ok -or $found.data.total_matches -ne 1) { throw 'Preset list is not uniquely accessible' }
                $scroll = agent-desktop scroll $found.data.matches[0].ref_id --direction down --amount 100 | ConvertFrom-Json
                if (!$scroll.ok) { throw ($scroll | ConvertTo-Json -Depth 8) }
                $verified = agent-desktop find --app LightCrosshair.UI.exe --native-id PresetList --timeout-ms 5000 | ConvertFrom-Json
                if (!$verified.ok -or $verified.data.total_matches -ne 1) { throw 'Preset list disappeared after scroll' }
                $scrolled = $true
            }
            Start-Sleep -Milliseconds 100
        }
        # The relay marks completion after forwarding every successful thumbnail reply.
        # Allow WPF to decode/paint before closing the actual Library window.
        Start-Sleep -Milliseconds 500
        $readyMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
        if (!$ui.CloseMainWindow() -or !$ui.WaitForExit(10000)) { throw "UI session $cycle did not close" }
        $ui.Dispose(); $ui = $null
        $thumbnailCount = $relay.Rendered
        $relay.Dispose(); $relay = $null
        Start-Sleep -Milliseconds 500
        if ($app.HasExited) { throw 'Core exited during soak' }
        $app.Refresh()
        $point = [pscustomobject]@{
            Session=$cycle; PrivateMiB=[math]::Round($app.PrivateMemorySize64 / 1MB, 3)
            WorkingSetMiB=[math]::Round($app.WorkingSet64 / 1MB, 3); Handles=$app.HandleCount
            GdiHandles=[PerfNative]::GetGuiResources($app.Handle, 0); UserHandles=[PerfNative]::GetGuiResources($app.Handle, 1)
            Thumbnails=$thumbnailCount; ReadyMilliseconds=$readyMs
        }
        $curve.Add($point)
        $curve | Export-Csv -LiteralPath "$OutputDirectory/curve.csv" -NoTypeInformation
        Write-Host "Session $cycle/$Sessions`: $($point.PrivateMiB) MiB; $thumbnailCount thumbnails; handles $($point.Handles)"
    }
    $summary = [ordered]@{
        MeasuredAt=[DateTime]::UtcNow.ToString('O'); Executable=[IO.Path]::GetFullPath($Executable)
        Sessions=$Sessions; TemporaryData=$root; PrivateBeforeMiB=[math]::Round($before / 1MB, 3)
        Curve=$curve; WpfLoadedInCore=([PerfNative]::Query($window, 0x8065) -ne 0)
        Method='Real UI --page library; transparent external pipe relay counts all successful 96px thumbnail replies; accessibility scroll through all presets; 500ms paint and 500ms post-exit settle. No injected render requests or forced GC in the curve.'
    }
    if ($DiagnosticsTools) {
        & (Join-Path $DiagnosticsTools 'dotnet-gcdump.exe') collect --process-id $app.Id --output "$OutputDirectory/final.gcdump"
        if ($LASTEXITCODE) { throw 'Heap collection failed' }
        & (Join-Path $DiagnosticsTools 'dotnet-gcdump.exe') report "$OutputDirectory/final.gcdump" | Set-Content "$OutputDirectory/final-heap.txt"
    }
}
finally {
    if ($ui) { if (!$ui.HasExited) { [void]$ui.CloseMainWindow(); if (!$ui.WaitForExit(5000)) { $ui.Kill(); $ui.WaitForExit() } }; $ui.Dispose() }
    if ($relay) { $relay.Dispose() }
    if ($app) {
        if (!$app.HasExited) {
            [void][PerfNative]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero)
            if (!$app.WaitForExit(10000)) { $app.Kill(); $app.WaitForExit() }
        }
        $app.Dispose()
    }
    $env:LIGHTCROSSHAIR_DATA_ROOT = $previousRoot
    $env:LIGHTCROSSHAIR_TEST_INSTANCE = $previousInstance
    Restore-RealProfileGuard $guard "$OutputDirectory/real-profile-verification.json"
}
if ($summary) {
    $summary.RealProfileVerified = $true
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$OutputDirectory/summary.json" -Encoding utf8
}
