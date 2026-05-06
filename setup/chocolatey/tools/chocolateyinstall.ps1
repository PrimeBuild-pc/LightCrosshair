$ErrorActionPreference = 'Stop'

# Package information
$packageName = 'lightcrosshair'
$version = '1.3.0'
$url64 = "https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v$version/LightCrosshair.exe"
$checksum64 = '7847452EFCBA0975C46DA43A0BA9BE0A52C8D67B3F0982E49BB194B6641BC46C'
$checksumType64 = 'sha256'

# Installation paths
$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$installDir = Join-Path $env:ProgramFiles 'LightCrosshair'

Write-Host "Installing $packageName v$version..."

# Create installation directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Download executable
$exePath = Join-Path $installDir 'LightCrosshair.exe'
Write-Host "Downloading LightCrosshair.exe from GitHub Releases..."

try {
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $url64 -OutFile $exePath -UseBasicParsing
    $ProgressPreference = 'Continue'
} catch {
    Write-Error "Failed to download from $url64. Please check your internet connection or visit the GitHub Releases page directly."
    exit 1
}

# Verify checksum (if available)
if ($checksum64 -and $checksum64 -ne 'CHECKSUM_TO_BE_FILLED') {
    Write-Host "Verifying file integrity..."
    $actualChecksum = (Get-FileHash -Path $exePath -Algorithm $checksumType64).Hash
    if ($actualChecksum -ne $checksum64) {
        Remove-Item $exePath -Force
        Write-Error "Checksum verification failed. Expected: $checksum64, Got: $actualChecksum"
        exit 1
    }
    Write-Host "Checksum verified successfully."
}

# Create shortcut in Start Menu
$startMenuDir = [System.IO.Path]::Combine($env:AppData, 'Microsoft\Windows\Start Menu\Programs\LightCrosshair')
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}

$shortcutPath = Join-Path $startMenuDir 'LightCrosshair.lnk'
$wshShell = New-Object -ComObject WScript.Shell
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'Lightweight gaming crosshair overlay'
$shortcut.IconLocation = $exePath
$shortcut.Save()

Write-Host "LightCrosshair installed successfully to: $installDir"
Write-Host "A shortcut has been created in your Start Menu."
Write-Host "Launch the application from Start Menu or run: $exePath"
