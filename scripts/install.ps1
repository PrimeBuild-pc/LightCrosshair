# Install an already downloaded and reviewed v2 portable ZIP. No network access.
param(
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$Sha256,
    [string]$InstallPath = "$env:LOCALAPPDATA/Programs/LightCrosshair"
)
$ErrorActionPreference = 'Stop'
$actual = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash
if ($actual -ne $Sha256) { throw 'Archive checksum mismatch' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    if ('LightCrosshair.exe' -notin $names -or 'LightCrosshair.UI.exe' -notin $names -or 'CORE-ONLY.txt' -in $names) { throw 'This is not a complete v2 package' }
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.Contains('..') -or [IO.Path]::IsPathRooted($entry.FullName)) { throw 'Invalid archive path' }
    }
}
finally { $zip.Dispose() }
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Expand-Archive -LiteralPath $Archive -DestinationPath $InstallPath -Force
Write-Host "Installed in $InstallPath. Requires .NET Desktop Runtime 10."
