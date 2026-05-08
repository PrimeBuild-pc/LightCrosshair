@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%.."
set "PROJECT_FILE=%REPO_ROOT%\LightCrosshair\LightCrosshair.csproj"
set "OUTPUT_ROOT=%REPO_ROOT%\releases"
set "SELF_CONTAINED=false"
set "PUBLISH_TRIMMED=false"

echo LightCrosshair Release Builder
echo ==============================

REM Check if .NET is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo Error: .NET SDK is not installed or not in PATH
    echo Please install .NET 8.0 SDK or later from https://dotnet.microsoft.com/download
    exit /b 1
)

REM Set version (can be overridden by setting VERSION environment variable)
if "%VERSION%"=="" set VERSION=1.4.0

if /I "%1"=="--self-contained" set "SELF_CONTAINED=true"

echo Building LightCrosshair v%VERSION%...
echo Self-contained: %SELF_CONTAINED%
echo PublishTrimmed: %PUBLISH_TRIMMED%
echo.

REM Clean previous builds
if exist "%OUTPUT_ROOT%" (
    echo Cleaning previous builds...
    rmdir /s /q "%OUTPUT_ROOT%"
)

REM Create output directory
mkdir "%OUTPUT_ROOT%"

REM Build x64 version
echo Building x64 release...
dotnet publish "%PROJECT_FILE%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained %SELF_CONTAINED% ^
    --output "%OUTPUT_ROOT%\x64" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:PublishTrimmed=%PUBLISH_TRIMMED% ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:Version=%VERSION% ^
    --verbosity minimal

if errorlevel 1 (
    echo Error: x64 build failed
    exit /b 1
)

REM Build ARM64 version
echo Building ARM64 release...
dotnet publish "%PROJECT_FILE%" ^
    --configuration Release ^
    --runtime win-arm64 ^
    --self-contained %SELF_CONTAINED% ^
    --output "%OUTPUT_ROOT%\ARM64" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:PublishTrimmed=%PUBLISH_TRIMMED% ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:Version=%VERSION% ^
    --verbosity minimal

if errorlevel 1 (
    echo Error: ARM64 build failed
    exit /b 1
)

echo.
echo Build completed successfully!
echo Output directory: %OUTPUT_ROOT%
echo.
echo Available builds:
echo   - x64: %OUTPUT_ROOT%\x64\LightCrosshair.exe
echo   - ARM64: %OUTPUT_ROOT%\ARM64\LightCrosshair.exe
echo.

REM Create ZIP packages if PowerShell is available
powershell -Command "Get-Command Compress-Archive" >nul 2>&1
if not errorlevel 1 (
    echo Creating ZIP packages...
    
    powershell -Command "Compress-Archive -Path '%OUTPUT_ROOT%\x64\*' -DestinationPath '%OUTPUT_ROOT%\LightCrosshair-v%VERSION%-x64.zip' -Force"
    powershell -Command "Compress-Archive -Path '%OUTPUT_ROOT%\ARM64\*' -DestinationPath '%OUTPUT_ROOT%\LightCrosshair-v%VERSION%-ARM64.zip' -Force"
    
    echo ZIP packages created:
    echo   - LightCrosshair-v%VERSION%-x64.zip
    echo   - LightCrosshair-v%VERSION%-ARM64.zip
) else (
    echo Note: PowerShell not available for ZIP creation
    echo You can manually create ZIP files from the x64 and ARM64 directories
)

echo.
exit /b 0
