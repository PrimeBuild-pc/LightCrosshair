<#
Lightweight publish script for LightCrosshair
Generates versioned single-file builds (win-x64) + SHA256 + zip + tag suggestion.
Usage:
  powershell -ExecutionPolicy Bypass -File .\publish.ps1 -Version 1.2.3[-prerelease]
Parameters:
  -Version       SemVer (required; pre-release labels allowed)
  -Runtime       Runtime RID (default win-x64)
  -Trim          Switch to enable trimming (experimental)
  -DryRun        Show actions without executing
#>
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Runtime = 'win-x64',
    [switch]$Trim,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'LightCrosshair'
$outRoot = Join-Path $root 'dist'
$outDir = Join-Path $outRoot "LightCrosshair-v$Version-$Runtime"

if (-not $DryRun) {
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

 # Sanitize numeric portion for assembly/file versions (strip pre-release label)
$numericVersion = ($Version -split '-')[0]
if (-not ($numericVersion -match '^\d+\.\d+\.\d+')) {
  throw "Version '$Version' must start with major.minor.patch (e.g. 1.2.3 or 1.2.3-beta)"
}

$assemblyFileVersion = "$numericVersion.0" # add 4th part

$props = @(
  "/p:PublishSingleFile=true",
  "/p:SelfContained=true",
  "/p:PublishReadyToRun=true",
  "/p:IncludeNativeLibrariesForSelfExtract=true",
  "/p:EnableCompressionInSingleFile=true",
  "/p:Version=$Version",
  "/p:AssemblyVersion=$assemblyFileVersion",
  "/p:FileVersion=$assemblyFileVersion",
  "/p:InformationalVersion=$Version"
)
if ($Trim) { $props += '/p:PublishTrimmed=true'; $props += '/p:TrimMode=partial' }

$publishArgs = @('publish', $proj, '--configuration','Release','--runtime',$Runtime,'--self-contained','true','--output',$outDir) + $props

Write-Host "Publishing LightCrosshair $Version ($Runtime)" -ForegroundColor Cyan
Write-Host "dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray
if (-not $DryRun) { & dotnet @publishArgs }

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Basic contents sanity
$exe = Join-Path $outDir 'LightCrosshair.exe'
if (-not (Test-Path $exe)) { throw 'Executable not found after publish.' }

# Hash + zip
$zipPath = Join-Path $outRoot "LightCrosshair-v$Version-$Runtime.zip"
$hashPath = $zipPath + '.sha256'
if (-not $DryRun) {
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force
    $hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash
    Set-Content -Path $hashPath -Value "$hash  $(Split-Path -Leaf $zipPath)" -Encoding ASCII
}

Write-Host "Output:" -ForegroundColor Green
Write-Host "  Directory: $outDir" -ForegroundColor Gray
Write-Host "  Zip:       $zipPath" -ForegroundColor Gray
Write-Host "  Hash:      $hashPath" -ForegroundColor Gray

# Release notes template
$notes = @"
## LightCrosshair v$Version

### Highlights
- (Add key changes here)

### Integrity
SHA256: $( if (-not $DryRun) { (Get-Content $hashPath) } else { '(dry-run hash pending)' })

### Run
Extract and run LightCrosshair.exe (no install needed).

### System Requirements
- Windows 10 1809+ / Windows 11
- No external .NET runtime required

"@
$notesPath = Join-Path $outRoot "RELEASE-NOTES-v$Version.md"
if (-not $DryRun) { Set-Content -Path $notesPath -Value $notes -Encoding UTF8 }
Write-Host "  Notes:     $notesPath" -ForegroundColor Gray

Write-Host "Suggested tag: v$Version" -ForegroundColor Yellow
Write-Host "Next: git add dist; git commit -m 'release: v$Version'; git tag v$Version" -ForegroundColor DarkCyan
