@echo off
REM Simple batch script to run the CLI project
REM Usage: run-cli.bat [Debug|Release] [CLI arguments...]

setlocal enabledelayedexpansion

set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug

echo.
echo 🔧 Starting CLI Application...
echo Configuration: %CONFIG%
echo.

cd /d "%~dp0"
dotnet build HomeSteadier.CLI --configuration %CONFIG%
dotnet run --project "HomeSteadier.CLI" --configuration %CONFIG% -- %*

pause
