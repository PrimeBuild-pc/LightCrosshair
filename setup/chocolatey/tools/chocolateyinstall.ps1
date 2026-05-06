$ErrorActionPreference = 'Stop'

# Package information
$packageName = 'lightcrosshair'
$version = '1.3.0'

# Installation paths
$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$installDir = Join-Path $env:ProgramFiles 'LightCrosshair'
$sourceExe = Join-Path $toolsDir 'LightCrosshair.exe'

Write-Host "Installing $packageName v$version..."

# Create installation directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Copy executable from package
$exePath = Join-Path $installDir 'LightCrosshair.exe'
Write-Host "Copying LightCrosshair.exe to installation directory..."

if (-not (Test-Path $sourceExe)) {
    Write-Error "LightCrosshair.exe not found in package at: $sourceExe"
    exit 1
}

Copy-Item -Path $sourceExe -Destination $exePath -Force
Write-Host "Executable copied successfully."

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
