<#
.SYNOPSIS
  Builds the Microsoft Store package: one MSIX per architecture (x64, ARM64) and an .msixbundle to upload.
.DESCRIPTION
  Core and UI are published self-contained into one folder (they share one copy of the .NET runtime), because
  the Store cannot install the .NET Desktop Runtime for a desktop app. The binaries are the same code as the
  ZIP/installer builds (build-release.ps1); packaged behavior is chosen at run time from the package identity.

  -Sign signs the bundle with a local self-signed test certificate whose subject equals the manifest Publisher,
  for sideload tests only. Do not sign the Store upload: Microsoft signs it after certification.

  Needs the Windows 10/11 SDK tools makeappx.exe, makepri.exe and signtool.exe
  (Visual Studio Installer > Individual components > "Windows 11 SDK", or winget install Microsoft.WindowsSDK.10.0.26100).
.EXAMPLE
  ./scripts/build-msix.ps1 -Version 2.0.0          # Store upload: releases/msix/LightCrosshair_2.0.0.0.msixbundle
  ./scripts/build-msix.ps1 -Version 2.0.0 -Sign    # sideload test bundle + LightCrosshair-test.cer
#>
param(
    [string]$Version = '2.0.0',
    [ValidateSet('win-x64', 'win-arm64')][string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [switch]$Sign
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be Major.Minor.Patch (the Store needs the fourth number to be 0).' }
$packageVersion = "$Version.0"
$out = Join-Path $repo 'releases/msix'
$source = Join-Path $repo 'packaging/msix'

function Find-SdkTool([string]$name) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
    $tool = Get-ChildItem -Path $kits -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "$hostArch/$name" } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$tool) { throw "$name not found. Install the Windows 11 SDK (winget install Microsoft.WindowsSDK.10.0.26100)." }
    $tool
}
function Invoke-Tool([string]$tool, [string[]]$arguments) {
    $output = & $tool @arguments 2>&1
    if ($LASTEXITCODE) { $output | Out-Host; throw "$([IO.Path]::GetFileName($tool)) failed with exit code $LASTEXITCODE" }
}
$makeAppx = Find-SdkTool 'makeappx.exe'
$makePri = Find-SdkTool 'makepri.exe'

[xml]$manifest = Get-Content -LiteralPath (Join-Path $source 'Package.appxmanifest') -Raw
$identity = $manifest.Package.Identity
if ($identity.Name -like '*Placeholder*' -or $identity.Publisher -like '*Placeholder*') {
    Write-Warning 'Package identity still has the placeholders. Fill it from Partner Center (packaging/msix/Package.appxmanifest) before uploading.'
}

if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$bundleInput = New-Item -ItemType Directory -Path (Join-Path $out 'bundle-input') -Force
foreach ($runtime in $Runtimes) {
    $arch = $runtime.Substring(4)
    $layout = Join-Path $out "layout-$arch"
    foreach ($project in @('LightCrosshair/LightCrosshair.csproj', 'LightCrosshair.UI/LightCrosshair.UI.csproj')) {
        $arguments = @('publish', (Join-Path $repo $project), '-c', 'Release', '-r', $runtime, '--self-contained', 'true', '-o', $layout,
            '-p:PublishSingleFile=false', '-p:PublishReadyToRun=true', '-p:PublishTrimmed=false',
            "-p:Version=$Version", "-p:AssemblyVersion=$packageVersion", "-p:FileVersion=$packageVersion",
            '-p:UseArtifactsOutput=true', "-p:ArtifactsPath=$out/build-artifacts", '-p:DebugType=none', '--nologo')
        Write-Host "dotnet $($arguments -join ' ')"
        & dotnet @arguments
        if ($LASTEXITCODE) { throw "Publish failed: $project ($runtime)" }
    }
    foreach ($name in @('LightCrosshair.exe', 'LightCrosshair.UI.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $layout $name))) { throw "Missing $name in $layout" }
    }

    # Manifest with this build's version and architecture; logos; resources.pri for the scaled logos.
    $identity.Version = $packageVersion
    $identity.ProcessorArchitecture = $arch
    $manifest.Save((Join-Path $layout 'AppxManifest.xml'))
    Copy-Item -LiteralPath (Join-Path $source 'Assets') -Destination (Join-Path $layout 'Assets') -Recurse -Force
    # Index only the logos: indexing the whole layout would treat the satellite-assembly folders (it, ja, ...) as languages.
    $priRoot = Join-Path $out "pri-$arch"
    New-Item -ItemType Directory -Path $priRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'Assets') -Destination (Join-Path $priRoot 'Assets') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Destination $priRoot
    Invoke-Tool $makePri @('createconfig', '/cf', "$priRoot/priconfig.xml", '/dq', 'en-US', '/pv', '10.0.0', '/o')
    # One resources.pri with every scale: without resource packs in the bundle, split .pri files would point nowhere.
    [xml]$priConfig = Get-Content -LiteralPath "$priRoot/priconfig.xml" -Raw
    $packaging = $priConfig.resources.SelectSingleNode('packaging')
    if ($packaging) { [void]$priConfig.resources.RemoveChild($packaging) }
    $priConfig.Save("$priRoot/priconfig.xml")
    Invoke-Tool $makePri @('new', '/pr', $priRoot, '/cf', "$priRoot/priconfig.xml", '/mn', "$priRoot/AppxManifest.xml", '/of', "$layout/resources.pri", '/o')

    $msix = Join-Path $bundleInput "LightCrosshair_${packageVersion}_$arch.msix"
    Invoke-Tool $makeAppx @('pack', '/d', $layout, '/p', $msix, '/o')
    Write-Host "Packed $msix"
    $layoutMb = [math]::Round(((Get-ChildItem -LiteralPath $layout -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 1)
    Write-Host ("{0}: layout {1} MB, msix {2} MB" -f $arch, $layoutMb, [math]::Round((Get-Item $msix).Length / 1MB, 1))
}

$bundle = Join-Path $out "LightCrosshair_$packageVersion.msixbundle"
Invoke-Tool $makeAppx @('bundle', '/d', $bundleInput, '/p', $bundle, '/bv', $packageVersion, '/o')

if ($Sign) {
    $signTool = Find-SdkTool 'signtool.exe'
    $publisher = $identity.Publisher
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $publisher -and $_.FriendlyName -eq 'LightCrosshair MSIX test' -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
    if (!$cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -FriendlyName 'LightCrosshair MSIX test' -KeyUsage DigitalSignature `
            -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(1) `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    Invoke-Tool $signTool @('sign', '/fd', 'SHA256', '/sha1', $cert.Thumbprint, '/s', 'My', $bundle)
    Export-Certificate -Cert $cert -FilePath (Join-Path $out 'LightCrosshair-test.cer') | Out-Null
    Write-Host 'Test-signed. To sideload: import LightCrosshair-test.cer into Local Machine > Trusted People, then open the bundle.'
}
Write-Host "Prepared $bundle ($([math]::Round((Get-Item $bundle).Length / 1MB, 1)) MB)"
