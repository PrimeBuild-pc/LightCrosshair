@echo off
setlocal

echo ==========================================
echo LightCrosshair - Uninstall Cleanup Utility
echo ==========================================
echo.

set "APPDATA_DIR=%AppData%\LightCrosshair"
set "LOCALAPPDATA_DIR=%LocalAppData%\LightCrosshair"
set "RUN_KEY_HKCU=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
set "RUN_KEY_HKLM=HKLM\Software\Microsoft\Windows\CurrentVersion\Run"
set "RUN_VALUE=LightCrosshair"

echo [1/5] Stopping running LightCrosshair processes...
taskkill /IM "LightCrosshair.exe" /F /T >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-CimInstance Win32_Process -Filter \"Name='LightCrosshair.exe'\" | ForEach-Object { Invoke-CimMethod -InputObject $_ -MethodName Terminate | Out-Null }" >nul 2>&1

echo [2/5] Removing roaming app data from:
echo     %APPDATA_DIR%
if exist "%APPDATA_DIR%" (
    rmdir /s /q "%APPDATA_DIR%"
    echo     Removed.
) else (
    echo     Not found.
)

echo [3/5] Removing local app data from:
echo     %LOCALAPPDATA_DIR%
if exist "%LOCALAPPDATA_DIR%" (
    rmdir /s /q "%LOCALAPPDATA_DIR%"
    echo     Removed.
) else (
    echo     Not found.
)

echo [4/5] Removing startup registry entry (Current User)...
reg delete "%RUN_KEY_HKCU%" /v "%RUN_VALUE%" /f >nul 2>&1
if errorlevel 1 (
    echo     No HKCU startup value found.
) else (
    echo     HKCU startup value removed.
)

echo [5/5] Attempting startup registry cleanup (All Users)...
reg delete "%RUN_KEY_HKLM%" /v "%RUN_VALUE%" /f >nul 2>&1
if errorlevel 1 (
    echo     No HKLM startup value removed (may require Administrator rights or value not present).
) else (
    echo     HKLM startup value removed.
)

echo.
echo Cleanup completed.
echo You can now delete the application executable/folder manually if desired.
echo.
pause
