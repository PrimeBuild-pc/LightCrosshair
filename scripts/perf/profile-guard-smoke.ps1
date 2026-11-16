$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/ProfileGuard.ps1"
$root = Join-Path ([IO.Path]::GetTempPath()) ('lc2-profile-guard-tests-' + [Guid]::NewGuid().ToString('N'))
foreach ($case in @('existing', 'missing', 'empty', 'foreign-change')) {
    $profile = Join-Path $root "$case/profile"
    $backup = Join-Path $root "$case/backup"
    $report = Join-Path $root "$case/report.json"
    New-Item -ItemType Directory -Force -Path (Join-Path $profile 'v2') | Out-Null
    if ($case -ne 'empty') { [IO.File]::WriteAllText((Join-Path $profile 'v2/store.json'), '{"original":true}') }
    if ($case -eq 'existing') { [IO.File]::WriteAllBytes((Join-Path $profile 'v2/ui-placement.json'), [byte[]]@(239,187,191,123,125,13,10)) }
    $guard = Start-RealProfileGuard $backup $profile
    [IO.File]::WriteAllText((Join-Path $profile 'v2/ui-placement.json'), '{"test":true}')
    if ($case -eq 'foreign-change') { [IO.File]::WriteAllText((Join-Path $profile 'v2/store.json'), '{"changed":true}') }
    $failed = $false
    try { Restore-RealProfileGuard $guard $report }
    catch { $failed = $true }
    if ($failed -ne ($case -eq 'foreign-change')) { throw "Unexpected guard outcome: $case" }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($result.AllFilePathsAndHashesUnchanged -ne ($case -ne 'foreign-change')) { throw "Incorrect manifest comparison: $case" }
    if ($case -eq 'existing' -and (Get-FileHash -LiteralPath $guard.Placement).Hash -ne (Get-FileHash -LiteralPath $guard.Backup).Hash) { throw 'Placement bytes were not restored' }
    if ($case -ne 'existing' -and (Test-Path -LiteralPath $guard.Placement)) { throw 'New placement file was not removed' }
    Write-Host "PASS: profile guard $case"
}
