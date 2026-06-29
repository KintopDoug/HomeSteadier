@echo off
REM Simple batch script to run the Aspire project
REM Usage: run-aspire.bat [Debug|Release]

setlocal enabledelayedexpansion

set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug

echo.
echo 🚀 Starting Aspire Application...
echo Configuration: %CONFIG%
echo.

cd /d "%~dp0"
dotnet clean
dotnet build
dotnet run --project "HomeSteadier.AppHost" --configuration %CONFIG%

pause
