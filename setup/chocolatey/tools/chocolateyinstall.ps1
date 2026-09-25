$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v1.8.0/LightCrosshair-Setup-1.8.0.exe'
$checksum = '1F25C2B6DF489568B8E65539DA5F2D82ECE57D65172669616AB45DF7C125B8A7'

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
