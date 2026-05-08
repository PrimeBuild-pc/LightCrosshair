<#
.SYNOPSIS
    LightCrosshair Install Script
    
.DESCRIPTION
    Downloads and installs LightCrosshair from GitHub Releases.
    This script can be run directly with:
      irm https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1 | iex
    
    Or with parameters:
      & ([scriptblock]::Create((irm https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1))) -Version 1.4.0 -InstallPath "C:\Program Files\LightCrosshair" -Checksum "<SHA256>"

.PARAMETER Version
    Version to install (default: 1.4.0)

.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\LightCrosshair)

.PARAMETER SkipChecksum
    Skip SHA256 verification (not recommended)

.PARAMETER Checksum
    Expected SHA256 for the downloaded executable. The release process should pass or embed this value after the final artifact is built.

.PARAMETER SkipStartMenu
    Skip creating Start Menu shortcut

.EXAMPLE
    irm https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1 | iex

.EXAMPLE
    powershell -Command "& ([scriptblock]::Create((irm https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1))) -Version 1.4.0 -Checksum '<SHA256>'"
#>

param(
    [string]$Version = '1.4.0',
    [string]$InstallPath = "$env:ProgramFiles\LightCrosshair",
    [string]$Checksum = '',
    [switch]$SkipChecksum,
    [switch]$SkipStartMenu
)

$ErrorActionPreference = 'Stop'

# Configuration
$packageName = 'LightCrosshair'
$releaseUrl = "https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v$Version/LightCrosshair.exe"
$knownChecksums = @{
}

if ([string]::IsNullOrWhiteSpace($Checksum) -and $knownChecksums.ContainsKey($Version)) {
    $Checksum = $knownChecksums[$Version]
}

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  $packageName Installation Script      ║" -ForegroundColor Cyan
Write-Host "║  Version: $Version                    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Validate installation path
if (-not (Test-Path (Split-Path $InstallPath))) {
    Write-Error "Parent directory does not exist: $(Split-Path $InstallPath)"
    exit 1
}

# Create installation directory
if (-not (Test-Path $InstallPath)) {
    Write-Host "[1/4] Creating installation directory: $InstallPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
} else {
    Write-Host "[1/4] Installation directory exists: $InstallPath" -ForegroundColor Yellow
}

$exePath = Join-Path $InstallPath 'LightCrosshair.exe'

# Download executable
Write-Host "[2/4] Downloading from GitHub Releases..." -ForegroundColor Yellow
Write-Host "      URL: $releaseUrl" -ForegroundColor Gray

try {
    $ProgressPreference = 'SilentlyContinue'
    $response = Invoke-WebRequest -Uri $releaseUrl -UseBasicParsing -ErrorAction Stop
    [System.IO.File]::WriteAllBytes($exePath, $response.Content)
    $ProgressPreference = 'Continue'
    Write-Host "      Downloaded: $(Get-FileHash $exePath -Algorithm SHA256 | Select-Object -ExpandProperty Hash)" -ForegroundColor Green
} catch {
    Write-Error "Failed to download from GitHub. Please check your internet connection or visit the releases page: https://github.com/PrimeBuild-pc/LightCrosshair/releases"
    exit 1
}

# Verify checksum
if (-not $SkipChecksum -and -not [string]::IsNullOrWhiteSpace($Checksum)) {
    Write-Host "[3/4] Verifying file integrity (SHA256)..." -ForegroundColor Yellow
    $actualHash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash
    
    if ($actualHash -eq $Checksum) {
        Write-Host "      ✓ Checksum verified successfully" -ForegroundColor Green
    } else {
        Remove-Item $exePath -Force
        Write-Error "Checksum verification failed!`n  Expected: $Checksum`n  Got:      $actualHash`n`nDownload may be corrupted. Please try again."
        exit 1
    }
} elseif (-not $SkipChecksum) {
    Write-Host "[3/4] Checksum verification unavailable for v$Version. Provide -Checksum after the release artifact is finalized." -ForegroundColor Yellow
} else {
    Write-Host "[3/4] Checksum verification skipped (not recommended)" -ForegroundColor Yellow
}

# Create Start Menu shortcut (unless skipped)
if (-not $SkipStartMenu) {
    Write-Host "[4/4] Creating Start Menu shortcut..." -ForegroundColor Yellow
    
    $startMenuDir = [System.IO.Path]::Combine($env:AppData, 'Microsoft\Windows\Start Menu\Programs\LightCrosshair')
    
    try {
        if (-not (Test-Path $startMenuDir)) {
            New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
        }
        
        $shortcutPath = Join-Path $startMenuDir 'LightCrosshair.lnk'
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $InstallPath
        $shortcut.Description = 'Lightweight gaming crosshair overlay'
        $shortcut.IconLocation = $exePath
        $shortcut.Save()
        
        Write-Host "      ✓ Shortcut created in Start Menu" -ForegroundColor Green
    } catch {
        Write-Host "      ⚠ Could not create Start Menu shortcut (non-fatal): $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "[4/4] Start Menu shortcut creation skipped" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Installation Completed Successfully! ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📍 Installation Path: $exePath" -ForegroundColor Cyan
Write-Host "🎮 Launch from: Start Menu → LightCrosshair" -ForegroundColor Cyan
Write-Host "   Or run: & '$exePath'" -ForegroundColor Cyan
Write-Host ""
Write-Host "⚙️  Default hotkey to open settings: Alt + L" -ForegroundColor Gray
Write-Host "📝 For help and documentation visit: https://github.com/PrimeBuild-pc/LightCrosshair#readme" -ForegroundColor Gray
