param([string]$OutputDirectory = "$PSScriptRoot/results-review3/probe")
$ErrorActionPreference = 'Stop'
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vs = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'Install Visual Studio C++ desktop tools and a Windows SDK.' }
$vcvars = Join-Path $vs 'VC/Auxiliary/Build/vcvars64.bat'
$source = Join-Path $PSScriptRoot 'SwapchainProbe.cpp'
$exe = Join-Path $out 'SwapchainProbe.exe'
$obj = Join-Path $out 'SwapchainProbe.obj'
$command = 'call "{0}" >nul && cl /nologo /W4 /WX /EHsc /std:c++17 /O2 /DUNICODE /D_UNICODE "{1}" /Fo"{2}" /Fe"{3}" /link d3d11.lib d3d12.lib dxgi.lib user32.lib' -f $vcvars, $source, $obj, $exe
& $env:ComSpec /d /s /c $command
if ($LASTEXITCODE) { throw 'Swapchain probe build failed' }
Write-Output $exe
