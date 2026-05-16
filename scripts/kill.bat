@echo off
if not "%~1"=="--run" (
    start "EqZero Kill" cmd /k ""%~f0"" --run
    exit /b
)
taskkill /F /IM EqZero.Server.exe 2>nul
taskkill /F /IM EqZero.Client.exe 2>nul
echo done
pause
