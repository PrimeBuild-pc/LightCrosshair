param([string]$Version = '2.0.1', [string]$UiProject, [switch]$CoreOnly, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
foreach ($runtime in @('win-x64', 'win-arm64')) {
    & "$PSScriptRoot/publish.ps1" -Version $Version -Runtime $runtime -UiProject $UiProject -OutputRoot releases -CoreOnly:$CoreOnly -DryRun:$DryRun
}
