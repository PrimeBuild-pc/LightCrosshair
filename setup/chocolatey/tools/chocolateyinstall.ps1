$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.7.0/LightCrosshair-Setup-1.7.0.exe'
$checksum = '82E4D878DF7881F5DE88C4A9444C200F18CE1BD14E0C88AFEF9C05099808090E'

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
