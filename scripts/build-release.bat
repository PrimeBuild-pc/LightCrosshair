@echo off
setlocal enabledelayedexpansion

echo LightCrosshair Release Builder
echo ==============================

REM Check if .NET is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo Error: .NET SDK is not installed or not in PATH
    echo Please install .NET 6.0 SDK or later from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Set version (can be overridden by setting VERSION environment variable)
if "%VERSION%"=="" set VERSION=1.0.0

echo Building LightCrosshair v%VERSION%...
echo.

REM Clean previous builds
if exist "releases" (
    echo Cleaning previous builds...
    rmdir /s /q "releases"
)

REM Create output directory
mkdir "releases"

REM Build x64 version
echo Building x64 release...
dotnet publish "LightCrosshair" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "releases\x64" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:Version=%VERSION% ^
    --verbosity minimal

if errorlevel 1 (
    echo Error: x64 build failed
    pause
    exit /b 1
)

REM Build ARM64 version
echo Building ARM64 release...
dotnet publish "LightCrosshair" ^
    --configuration Release ^
    --runtime win-arm64 ^
    --self-contained true ^
    --output "releases\ARM64" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:Version=%VERSION% ^
    --verbosity minimal

if errorlevel 1 (
    echo Error: ARM64 build failed
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo Output directory: releases\
echo.
echo Available builds:
echo   - x64: releases\x64\LightCrosshair.exe
echo   - ARM64: releases\ARM64\LightCrosshair.exe
echo.

REM Create ZIP packages if PowerShell is available
powershell -Command "Get-Command Compress-Archive" >nul 2>&1
if not errorlevel 1 (
    echo Creating ZIP packages...
    
    powershell -Command "Compress-Archive -Path 'releases\x64\*' -DestinationPath 'releases\LightCrosshair-v%VERSION%-x64.zip' -Force"
    powershell -Command "Compress-Archive -Path 'releases\ARM64\*' -DestinationPath 'releases\LightCrosshair-v%VERSION%-ARM64.zip' -Force"
    
    echo ZIP packages created:
    echo   - LightCrosshair-v%VERSION%-x64.zip
    echo   - LightCrosshair-v%VERSION%-ARM64.zip
) else (
    echo Note: PowerShell not available for ZIP creation
    echo You can manually create ZIP files from the x64 and ARM64 directories
)

echo.
pause
