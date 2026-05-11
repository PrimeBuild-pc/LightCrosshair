$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.5.0/LightCrosshair-Setup-1.5.0.exe'
$checksum = '60F334865960AB20F2DBD1F337BF33BE9AC237E7709E77DB495D5C6050B8B416'

$packageArgs = @{
    PackageName    = $packageName
    FileType       = 'exe'
    Url            = $installerUrl
    Checksum       = $checksum
    ChecksumType   = 'sha256'
    SilentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
    ValidExitCodes = @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
