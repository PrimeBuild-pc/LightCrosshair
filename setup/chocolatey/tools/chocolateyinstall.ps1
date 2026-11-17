$ErrorActionPreference = 'Stop'

$packageName = 'lightcrosshair'
$installerUrl = 'https://github.com/PrimeBuild-pc/LightCrosshair/releases/download/v2.0.0/LightCrosshair-Setup-2.0.0.exe'
$checksum = 'REPLACE_WITH_FINAL_SHA256'
if ($checksum -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Finalize the release checksum before packaging' }

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
