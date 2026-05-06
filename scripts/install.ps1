<#
.SYNOPSIS
    LightCrosshair Install Script
    
.DESCRIPTION
    Downloads and installs LightCrosshair from GitHub Releases.
    This script can be run directly with:
      iwr -useb https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1 | iex
    
    Or with parameters:
      & ([scriptblock]::Create((iwr -useb https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1))) -Version 1.3.0 -InstallPath "C:\Program Files\LightCrosshair"

.PARAMETER Version
    Version to install (default: 1.3.0)

.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\LightCrosshair)

.PARAMETER SkipChecksum
    Skip SHA256 verification (not recommended)

.PARAMETER SkipStartMenu
    Skip creating Start Menu shortcut

.EXAMPLE
    iwr -useb https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1 | iex

.EXAMPLE
    powershell -Command "& ([scriptblock]::Create((iwr -useb https://github.com/PrimeBuild-pc/LightCrosshair/raw/main/scripts/install.ps1))) -Version 1.3.0"
#>

param(
    [string]$Version = '1.3.0',
    [string]$InstallPath = "$env:ProgramFiles\LightCrosshair",
    [switch]$SkipChecksum,
    [switch]$SkipStartMenu
)

$ErrorActionPreference = 'Stop'

# Configuration
$packageName = 'LightCrosshair'
$releaseUrl = "https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v$Version/LightCrosshair.exe"
$sha256Hash = '7847452EFCBA0975C46DA43A0BA9BE0A52C8D67B3F0982E49BB194B6641BC46C'

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
if (-not $SkipChecksum) {
    Write-Host "[3/4] Verifying file integrity (SHA256)..." -ForegroundColor Yellow
    $actualHash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash
    
    if ($actualHash -eq $sha256Hash) {
        Write-Host "      ✓ Checksum verified successfully" -ForegroundColor Green
    } else {
        Remove-Item $exePath -Force
        Write-Error "Checksum verification failed!`n  Expected: $sha256Hash`n  Got:      $actualHash`n`nDownload may be corrupted. Please try again."
        exit 1
    }
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
