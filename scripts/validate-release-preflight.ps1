param([string]$ExpectedVersion = '2.0.1', [switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = Get-Content -LiteralPath "$repo/LightCrosshair/LightCrosshair.csproj"
if ($project.Project.PropertyGroup.Version -notcontains $ExpectedVersion) { throw 'Version mismatch' }
if ($project.Project.PropertyGroup.TargetFramework -notcontains 'net10.0-windows') { throw 'Core must target .NET 10' }
if ($project.Project.PropertyGroup.UseWPF -contains 'true') { throw 'Core must not enable WPF' }
dotnet build "$repo/LightCrosshair/LightCrosshair.csproj" -c Release --nologo
if ($LASTEXITCODE) { throw 'Core build failed' }
dotnet test "$repo/LightCrosshair.Tests/LightCrosshair.Tests.csproj" -c Release --nologo
if ($LASTEXITCODE) { throw 'Core tests failed' }
if (!$CoreOnly) {
    if (!(Test-Path -LiteralPath "$repo/LightCrosshair.UI/LightCrosshair.UI.csproj")) { throw 'UI project must be merged before a release' }
    dotnet test "$repo/LightCrosshair.UI.Tests/LightCrosshair.UI.Tests.csproj" -c Release --nologo
    if ($LASTEXITCODE) { throw 'UI tests failed' }
}
Write-Host 'Local checks passed. Signing, clean install and final UI/Core integration are release checks.'
