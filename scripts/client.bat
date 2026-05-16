@echo off
if not "%~1"=="--run" (
    start "EqZero Client" cmd /k ""%~f0"" --run
    exit /b
)
setlocal
cd /d %~dp0..
dotnet run --project src/Client --no-build
endlocal
