@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "deck=%~1"
set "dialog=%~2"
if not defined dialog exit /b 1
shift /1
shift /1

set "pauseOnExit="
if "%~1"=="" set "pauseOnExit=1"
set "windbot=%~dp0.."
set "runtime=%windbot%\bin\Debug"

set "builder="
for /f "delims=" %%I in ('where msbuild.exe 2^>nul') do if not defined builder set "builder=%%I"
if defined builder goto :build
set "vswhere=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%vswhere%" for /f "usebackq delims=" %%I in (`"%vswhere%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined builder set "builder=%%I"
if not defined builder (
    echo MSBuild was not found. Install Visual Studio Build Tools with .NET desktop build tools.
    goto :failure
)

:build
"%builder%" "%windbot%\WindBot.sln" /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
if errorlevel 1 goto :failure
if /i "%~1"=="--build-only" exit /b 0

set "serverHost=%~1"
if not defined serverHost set "serverHost=127.0.0.1"
set "port=%~2"
if not defined port set "port=7911"
set "database=%~f3"
if not defined database set "database=%windbot%\..\DeckWorkshop\Data\standard\cards.cdb"
if not exist "%database%" (
    echo Cannot find "%database%". Pass the cards.cdb path as the third launcher argument.
    goto :failure
)

set "stamp=%RANDOM%-%RANDOM%"
for /f "delims=" %%I in ('powershell.exe -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss-fffffff"') do set "stamp=%%I"
set "logPath=%windbot%\..\Artifacts\executor-logs\%deck%-%stamp%.log"
echo Decision log "%logPath%"
pushd "%runtime%" || goto :failure
WindBot.exe "Name=%deck%" "Deck=%deck%" "Dialog=%dialog%" "DbPath=%database%" "Host=%serverHost%" "Port=%port%" "HostInfo=%~4" Debug=True "LogPath=%logPath%"
set "botExit=%ERRORLEVEL%"
popd
if defined pauseOnExit pause
exit /b %botExit%

:failure
if defined pauseOnExit pause
exit /b 1
