@echo off
setlocal EnableExtensions
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-SkyStriker2026.ps1" %*
set "SKY_RESULT=%ERRORLEVEL%"
if not "%SKY_RESULT%"=="0" echo FAILED: inspect the reported receipt directory.
if "%~1"=="" pause
exit /b %SKY_RESULT%
