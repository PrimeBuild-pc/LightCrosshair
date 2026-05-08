# LightCrosshair 1.4.0 release preflight validation.
# Performs local, non-publishing checks only.

param(
    [string]$ExpectedVersion = "1.4.0"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..")).Path

$Failures = New-Object System.Collections.Generic.List[string]
$Warnings = New-Object System.Collections.Generic.List[string]
$Passed = New-Object System.Collections.Generic.List[string]

function Add-Pass([string]$Message) {
    $Passed.Add($Message) | Out-Null
}

function Add-Fail([string]$Message) {
    $Failures.Add($Message) | Out-Null
}

function Add-Warn([string]$Message) {
    $Warnings.Add($Message) | Out-Null
}

function Read-RepoFile([string]$RelativePath) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Fail "Missing required file: $RelativePath"
        return ""
    }

    return Get-Content -LiteralPath $path -Raw
}

function Assert-Contains([string]$Name, [string]$Content, [string]$Pattern, [string]$Message) {
    if ($Content -notmatch $Pattern) {
        Add-Fail "${Name}: $Message"
    }
    else {
        Add-Pass "${Name}: $Message"
    }
}

function Assert-DoesNotContain([string]$Name, [string]$Content, [string]$Pattern, [string]$Message) {
    if ($Content -match $Pattern) {
        Add-Fail "${Name}: $Message"
    }
    else {
        Add-Pass "${Name}: $Message"
    }
}

Push-Location $RepoRoot
try {
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
        Add-Fail "Could not determine current Git branch."
    }
    elseif ($branch -in @("main", "master")) {
        Add-Fail "Current branch is '$branch'. Release preflight must not run on main/master."
    }
    else {
        Add-Pass "Current branch '$branch' is not main/master."
    }

    $status = & git status --short
    if ($LASTEXITCODE -eq 0 -and $status) {
        Add-Warn "Working tree is dirty. This is allowed during local preflight, but review the diff before committing."
    }
    elseif ($LASTEXITCODE -eq 0) {
        Add-Pass "Working tree is clean."
    }
    else {
        Add-Fail "Could not inspect Git working tree status."
    }
}
finally {
    Pop-Location
}

$csproj = Read-RepoFile "LightCrosshair/LightCrosshair.csproj"
$buildScript = Read-RepoFile "scripts/build-release.ps1"
$readme = Read-RepoFile "README.md"
$releasePrep = Read-RepoFile "setup/RELEASE_PREP_1.4.0.md"
$inno = Read-RepoFile "setup/LightCrosshair.iss"
$nuspec = Read-RepoFile "setup/chocolatey/LightCrosshair.nuspec"
$wingetSubmission = Read-RepoFile "setup/WINGET_SUBMISSION.md"

Assert-Contains "LightCrosshair.csproj" $csproj "<Version>$([regex]::Escape($ExpectedVersion))</Version>" "project version is $ExpectedVersion"
Assert-Contains "LightCrosshair.csproj" $csproj "<AssemblyVersion>$([regex]::Escape($ExpectedVersion)).0</AssemblyVersion>" "assembly version is $ExpectedVersion.0"
Assert-Contains "LightCrosshair.csproj" $csproj "<FileVersion>$([regex]::Escape($ExpectedVersion)).0</FileVersion>" "file version is $ExpectedVersion.0"
Assert-Contains "scripts/build-release.ps1" $buildScript "\[string\]\`$Version\s*=\s*`"$([regex]::Escape($ExpectedVersion))`"" "default release version is $ExpectedVersion"
Assert-Contains "scripts/build-release.ps1" $buildScript "--self-contained" "publish command declares self-contained mode explicitly"
Assert-Contains "scripts/build-release.ps1" $buildScript 'if \(\$SelfContained\) \{ "true" \} else \{ "false" \}' "default publish mode remains framework-dependent unless -SelfContained is passed"
Assert-Contains "scripts/build-release.ps1" $buildScript '/p:EnableCompressionInSingleFile=\$selfContainedValue' "single-file compression follows self-contained mode"
Assert-Contains "setup/LightCrosshair.iss" $inno "AppVersion=$([regex]::Escape($ExpectedVersion))" "installer version is $ExpectedVersion"
Assert-Contains "setup/chocolatey/LightCrosshair.nuspec" $nuspec "<version>$([regex]::Escape($ExpectedVersion))</version>" "Chocolatey nuspec version is $ExpectedVersion"

$publicReleaseDocs = @{
    "README.md" = $readme
    "setup/RELEASE_PREP_1.4.0.md" = $releasePrep
    "setup/chocolatey/LightCrosshair.nuspec" = $nuspec
    "setup/WINGET_SUBMISSION.md" = $wingetSubmission
}

foreach ($entry in $publicReleaseDocs.GetEnumerator()) {
    Assert-DoesNotContain $entry.Key $entry.Value "(?i)choco\s+install\s+lightcrosshair" "must not advertise a live Chocolatey install command before publication"
    Assert-DoesNotContain $entry.Key $entry.Value "(?i)winget\s+install\s+PrimeBuild\.LightCrosshair" "must not advertise a live WinGet install command before publication"
    Assert-DoesNotContain $entry.Key $entry.Value "(?i)irm\s+https://github\.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install\.ps1\s*\|\s*iex" "must not advertise a live hosted PowerShell install command before publication"
    Assert-DoesNotContain $entry.Key $entry.Value "(?i)ETW/PresentMon" "must not claim ETW/PresentMon runtime support"
    Assert-DoesNotContain $entry.Key $entry.Value "(?i)PresentMon\s+(runtime\s+support|backend)" "must not claim PresentMon runtime backend support"
}

$packagingDocs = $readme + "`n" + $releasePrep + "`n" + $inno + "`n" + $nuspec
Assert-Contains "packaging docs" $packagingDocs "(?i)framework-dependent" "state framework-dependent output"
Assert-Contains "packaging docs" $packagingDocs "(?i)\.NET\s+8(\.0)?" "state .NET 8 runtime requirement"
Assert-Contains "packaging docs" $packagingDocs "(?i)Windows\s+Desktop\s+Runtime|Desktop\s+Runtime" "state Windows Desktop Runtime requirement"
Assert-Contains "setup/LightCrosshair.iss" $inno "(?i)Desktop\s+Runtime" "mentions Desktop Runtime"
Assert-Contains "setup/chocolatey/LightCrosshair.nuspec" $nuspec "dotnet-8\.0-desktopruntime" "depends on .NET 8 Desktop Runtime package"
Assert-Contains "setup/chocolatey/LightCrosshair.nuspec" $nuspec "(?i)not final until release artifacts and checksums are approved" "marks Chocolatey package as not final"
Assert-Contains "setup/WINGET_SUBMISSION.md" $wingetSubmission "(?i)final .*URL" "gates WinGet URL on final artifact"
Assert-Contains "setup/WINGET_SUBMISSION.md" $wingetSubmission "(?i)SHA256" "gates WinGet hash/checksum on final artifact"
Assert-Contains "setup/WINGET_SUBMISSION.md" $wingetSubmission "(?i)Do not submit" "forbids WinGet submission before approval"

$winget140Path = Join-Path $RepoRoot "setup/winget/manifests/p/PrimeBuild/LightCrosshair/$ExpectedVersion"
if (Test-Path -LiteralPath $winget140Path) {
    $manifestText = Get-ChildItem -LiteralPath $winget140Path -File -Recurse |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } |
        Out-String
    Assert-Contains "WinGet $ExpectedVersion manifests" $manifestText "PackageVersion:\s*$([regex]::Escape($ExpectedVersion))" "use expected package version"
    Assert-DoesNotContain "WinGet $ExpectedVersion manifests" $manifestText "(?i)InstallerUrl:\s*(TODO|TBD|<|https://example\.com)" "do not contain placeholder installer URLs if manifests exist"
    Assert-DoesNotContain "WinGet $ExpectedVersion manifests" $manifestText "(?i)InstallerSha256:\s*(TODO|TBD|<|0{16,})" "do not contain placeholder installer hashes if manifests exist"
}
else {
    Add-Pass "WinGet $ExpectedVersion manifests are not present; submission remains gated until final URL and SHA256 exist."
}

Write-Host "LightCrosshair $ExpectedVersion release preflight" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
foreach ($message in $Passed) {
    Write-Host "PASS  $message" -ForegroundColor Green
}
foreach ($message in $Warnings) {
    Write-Host "WARN  $message" -ForegroundColor Yellow
}
foreach ($message in $Failures) {
    Write-Host "FAIL  $message" -ForegroundColor Red
}

Write-Host ""
Write-Host "Summary: $($Passed.Count) passed, $($Warnings.Count) warnings, $($Failures.Count) failures"

if ($Failures.Count -gt 0) {
    exit 1
}

exit 0
