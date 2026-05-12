$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild/LightCrosshair/releases/download/v1.6.0/LightCrosshair-Setup-1.6.0.exe'
$checksum = '414E50D3A6E24F107A48CF7A35E6C7E06A9CA4E2C3527912CC79C2BBF723EDD8'

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
