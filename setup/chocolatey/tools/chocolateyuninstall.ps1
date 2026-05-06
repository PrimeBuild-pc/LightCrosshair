$ErrorActionPreference = 'SilentlyContinue'

# Package information
$packageName = 'lightcrosshair'
$installDir = Join-Path $env:ProgramFiles 'LightCrosshair'

Write-Host "Uninstalling $packageName..."

# Remove executable
$exePath = Join-Path $installDir 'LightCrosshair.exe'
if (Test-Path $exePath) {
    try {
        Remove-Item $exePath -Force
        Write-Host "Removed: $exePath"
    } catch {
        Write-Warning "Could not remove $exePath. It may still be running."
    }
}

# Remove installation directory if empty
if ((Test-Path $installDir) -and ((Get-ChildItem $installDir -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)) {
    Remove-Item $installDir -Force
    Write-Host "Removed installation directory: $installDir"
}

# Remove Start Menu shortcuts
$startMenuDir = [System.IO.Path]::Combine($env:AppData, 'Microsoft\Windows\Start Menu\Programs\LightCrosshair')
if (Test-Path $startMenuDir) {
    Remove-Item $startMenuDir -Recurse -Force
    Write-Host "Removed Start Menu shortcuts"
}

# Note: AppData config is intentionally left behind (user preference to keep settings)
Write-Host "Uninstall complete. User configuration saved in %AppData%\LightCrosshair (can be manually deleted if desired)."
