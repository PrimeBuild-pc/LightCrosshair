$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.5.0/LightCrosshair-Setup-1.5.0.exe'
$checksum = 'e79186a1dffdd2223bf694a2c3c6b7c21a7f61d4ab6c47d695f3a9e15db26d21'

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
