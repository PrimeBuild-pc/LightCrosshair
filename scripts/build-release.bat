@echo off
setlocal
if "%VERSION%"=="" set "VERSION=2.0.0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" -Version "%VERSION%" %*
exit /b %errorlevel%
