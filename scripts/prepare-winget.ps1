param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$InstallerUrl,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$Sha256,
    [Parameter(Mandatory)][ValidatePattern('^\d{4}-\d{2}-\d{2}$')][string]$ReleaseDate,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if ($InstallerUrl -notmatch '^https://github\.com/PrimeBuild-pc/LightCrosshair/releases/download/v[0-9.]+/LightCrosshair-Setup-[0-9.]+\.exe$') { throw 'Expected an official release installer URL' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$template = Get-Content -LiteralPath "$PSScriptRoot/../setup/winget/PrimeBuild.LightCrosshair.installer.yaml.template" -Raw
$template = $template.Replace('2.0.0', $Version).Replace('REPLACE_WITH_FINAL_SHA256', $Sha256)
$template = $template -replace '(?m)^    InstallerUrl:.*', "    InstallerUrl: $InstallerUrl"
$template = $template.Replace('ManifestType: installer', "ReleaseDate: $ReleaseDate`nManifestType: installer")
Set-Content -LiteralPath "$OutputDirectory/PrimeBuild.LightCrosshair.installer.yaml" -Value $template -Encoding utf8
@"
PackageIdentifier: PrimeBuild.LightCrosshair
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@ | Set-Content -LiteralPath "$OutputDirectory/PrimeBuild.LightCrosshair.yaml" -Encoding utf8
@"
PackageIdentifier: PrimeBuild.LightCrosshair
PackageVersion: $Version
PackageLocale: en-US
Publisher: PrimeBuild
PackageName: LightCrosshair
License: MIT
ShortDescription: Layered crosshair overlay for Windows
PackageUrl: https://github.com/PrimeBuild-pc/LightCrosshair
ManifestType: defaultLocale
ManifestVersion: 1.6.0
"@ | Set-Content -LiteralPath "$OutputDirectory/PrimeBuild.LightCrosshair.locale.en-US.yaml" -Encoding utf8
