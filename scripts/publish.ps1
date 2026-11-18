param(
    [string]$Version = '2.0.1',
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [string]$UiProject,
    [ValidateSet('dist', 'releases')][string]$OutputRoot = 'dist',
    [switch]$CoreOnly,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Invalid version' }
if (!$UiProject) { $UiProject = Join-Path $repo 'LightCrosshair.UI/LightCrosshair.UI.csproj' }
if (!$CoreOnly -and !(Test-Path -LiteralPath $UiProject)) { throw 'UI project missing. Supply -UiProject, or use -CoreOnly for development validation.' }
$outputBase = [IO.Path]::GetFullPath((Join-Path $repo $OutputRoot))
$output = [IO.Path]::GetFullPath((Join-Path $outputBase "LightCrosshair-v$Version-$Runtime"))
if (!$output.StartsWith($outputBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output escaped the package directory' }
if (!$DryRun) {
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
}
$projects = @((Join-Path $repo 'LightCrosshair/LightCrosshair.csproj'))
if (!$CoreOnly) { $projects += $UiProject }
foreach ($project in $projects) {
    $arguments = @('publish', $project, '-c', 'Release', '-r', $Runtime, '--self-contained', 'false', '-o', $output,
        '-p:PublishSingleFile=true', '-p:PublishReadyToRun=true', '-p:PublishTrimmed=false',
        '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=false',
        "-p:Version=$Version", "-p:AssemblyVersion=$(($Version -split '-')[0]).0", "-p:FileVersion=$(($Version -split '-')[0]).0",
        '-p:UseArtifactsOutput=true', "-p:ArtifactsPath=$outputBase/build-artifacts", '-p:DebugType=none', '--nologo')
    Write-Host "dotnet $($arguments -join ' ')"
    if (!$DryRun) { & dotnet @arguments; if ($LASTEXITCODE) { throw "Publish failed: $project" } }
}
if (!$DryRun) {
    $expected = if ($CoreOnly) { @('LightCrosshair.exe') } else { @('LightCrosshair.exe', 'LightCrosshair.UI.exe') }
    foreach ($name in $expected) {
        if (!(Test-Path -LiteralPath (Join-Path $output $name))) { throw "Missing $name" }
    }
    if ($CoreOnly) { Set-Content -LiteralPath (Join-Path $output 'CORE-ONLY.txt') -Value 'Development Core build. Not a complete distributable package.' }
    Set-Content -LiteralPath (Join-Path $output 'RUNTIME.txt') -Value '.NET Desktop Runtime 10 is required. Keep both executables together.'
    $zip = "$output.zip"
    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
    Write-Host "Prepared $zip"
}
