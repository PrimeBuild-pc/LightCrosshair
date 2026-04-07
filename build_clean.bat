@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "APPDATA_DIR=%AppData%\LightCrosshair"
set "LOCALAPPDATA_DIR=%LocalAppData%\LightCrosshair"
set "BUILD_OUTPUT=%SCRIPT_DIR%BuildOutput"

pushd "%SCRIPT_DIR%" >nul

echo [0/4] Stopping running LightCrosshair processes...
taskkill /IM "LightCrosshair.exe" /F /T >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_Process -Filter \"Name='LightCrosshair.exe'\" | ForEach-Object { Invoke-CimMethod -InputObject $_ -MethodName Terminate | Out-Null }" >nul 2>&1

echo [1/4] Resetting local app configuration...
if exist "%APPDATA_DIR%" (
    rmdir /s /q "%APPDATA_DIR%"
)
if exist "%LOCALAPPDATA_DIR%" (
    rmdir /s /q "%LOCALAPPDATA_DIR%"
)

echo [2/4] Running dotnet clean...
dotnet clean "LightCrosshair.sln"
if errorlevel 1 (
    echo dotnet clean failed.
    popd >nul
    exit /b 1
)

echo [3/4] Cleaning BuildOutput directory...
if exist "%BUILD_OUTPUT%" (
    rmdir /s /q "%BUILD_OUTPUT%"
)

echo [4/4] Publishing release build to BuildOutput...
dotnet publish "LightCrosshair\LightCrosshair.csproj" -c Release -o ".\BuildOutput\"
if errorlevel 1 (
    echo dotnet publish failed.
    popd >nul
    exit /b 1
)

echo.
echo Done. Fresh executable available in BuildOutput\

popd >nul
exit /b 0
