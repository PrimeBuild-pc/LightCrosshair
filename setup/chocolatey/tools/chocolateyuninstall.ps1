$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$softwareName = 'LightCrosshair*'
$uninstallKeys = @(Get-UninstallRegistryKey -SoftwareName $softwareName)

if ($uninstallKeys.Count -eq 0) {
    Write-Warning "LightCrosshair uninstall entry was not found. It may already be removed."
    return
}

foreach ($key in $uninstallKeys) {
    $uninstallString = $key.UninstallString
    if ([string]::IsNullOrWhiteSpace($uninstallString)) {
        Write-Warning "LightCrosshair uninstall entry has no uninstall command: $($key.DisplayName)"
        continue
    }

    $file = $uninstallString.Trim('"')
    $packageArgs = @{
        PackageName    = $packageName
        FileType       = 'exe'
        SilentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
        ValidExitCodes = @(0)
        File           = $file
    }

    Uninstall-ChocolateyPackage @packageArgs
}
