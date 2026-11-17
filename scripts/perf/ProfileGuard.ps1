# Only ui-placement.json may be written by an unisolated UI. Everything else is read/hash-only.
function Get-RealProfileManifest([string]$Root) {
    $manifest = [ordered]@{}
    if (Test-Path -LiteralPath $Root) {
        foreach ($file in (Get-ChildItem -LiteralPath $Root -File -Recurse -Force | Sort-Object FullName)) {
            $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
            $manifest[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return $manifest
}
function Start-RealProfileGuard([string]$BackupDirectory, [string]$ProfileRoot) {
    $root = if ($ProfileRoot) { [IO.Path]::GetFullPath($ProfileRoot) } else { Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'LightCrosshair' }
    $placement = [IO.Path]::GetFullPath((Join-Path $root 'v2/ui-placement.json'))
    New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
    $backup = Join-Path ([IO.Path]::GetFullPath($BackupDirectory)) 'ui-placement.original.json'
    if ($backup.StartsWith([IO.Path]::GetFullPath($root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Profile backup must be outside the real profile' }
    $before = Get-RealProfileManifest $root
    $existed = Test-Path -LiteralPath $placement -PathType Leaf
    if ($existed) { Copy-Item -LiteralPath $placement -Destination $backup }
    [pscustomobject]@{ Root=$root; Placement=$placement; Backup=$backup; Existed=$existed; Before=$before }
}
function Restore-RealProfileGuard($Guard, [string]$ReportPath) {
    if (!$Guard) { return }
    if ($Guard.Existed) { Copy-Item -LiteralPath $Guard.Backup -Destination $Guard.Placement -Force }
    elseif (Test-Path -LiteralPath $Guard.Placement) { Remove-Item -LiteralPath $Guard.Placement -Force }
    $after = Get-RealProfileManifest $Guard.Root
    $differences = [Collections.Generic.List[string]]::new()
    foreach ($key in $Guard.Before.Keys) {
        if (!$after.Contains($key)) { $differences.Add("Removed: $key") }
        elseif ($Guard.Before[$key] -cne $after[$key]) { $differences.Add("Changed: $key") }
    }
    foreach ($key in $after.Keys) { if (!$Guard.Before.Contains($key)) { $differences.Add("Added: $key") } }
    $report = [ordered]@{
        PlacementExistedBefore=$Guard.Existed; PlacementRestored=$true
        AllFilePathsAndHashesUnchanged=($differences.Count -eq 0)
        Before=$Guard.Before; After=$after; Differences=$differences
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8
    if ($differences.Count) { throw "Real profile file list/hashes changed; see $ReportPath" }
    Write-Host "PASS: real profile file list and SHA256 hashes unchanged; ui-placement.json restored (existed=$($Guard.Existed))"
}
