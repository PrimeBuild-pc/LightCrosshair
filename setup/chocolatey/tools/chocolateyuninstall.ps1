$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$softwareName = 'LightCrosshair*'
$uninstallKeys = @(Get-UninstallRegistryKey -SoftwareName $softwareName)

if ($uninstallKeys.Count -eq 0) {
    Write-Warning "LightCrosshair uninstall entry was not found. It may already be removed."
    return
}

foreach ($key in $uninstallKeys) {
    $uninstallString = if (-not [string]::IsNullOrWhiteSpace($key.QuietUninstallString)) {
        $key.QuietUninstallString
    } else {
        $key.UninstallString
    }

    if ([string]::IsNullOrWhiteSpace($uninstallString)) {
        Write-Warning "LightCrosshair uninstall entry has no uninstall command: $($key.DisplayName)"
        continue
    }

    $file = if ($uninstallString -match '^\s*"([^"]+)"') {
        $matches[1]
    } else {
        ($uninstallString -split '\s+', 2)[0]
    }

    if (-not (Test-Path -LiteralPath $file)) {
        Write-Warning "LightCrosshair uninstaller was not found: $file"
        continue
    }

    $packageArgs = @{
        PackageName    = $packageName
        FileType       = 'exe'
        SilentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
        ValidExitCodes = @(0, 3010)
        File           = $file
    }

    Uninstall-ChocolateyPackage @packageArgs
}
