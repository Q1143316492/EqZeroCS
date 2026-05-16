@echo off
if not "%~1"=="--run" (
    start "EqZero Server" cmd /k ""%~f0"" --run
    exit /b
)
REM Launches all server processes in separate console windows.
REM Usage: just double-click, or run `scripts\server.bat` from anywhere.

setlocal
cd /d %~dp0..

echo [server.bat] building solution...
dotnet build EqZeroCS.sln -nologo -v minimal
if errorlevel 1 (
    echo [server.bat] build FAILED
    pause
    exit /b 1
)

REM Boot order: backends -> gates -> login. Each process retries outbound dials,
REM so this order is a hint rather than a hard requirement.
start "EqZero gcc1"  cmd /k dotnet run --project src/Server --no-build -- --name gcc1
start "EqZero ats1"  cmd /k dotnet run --project src/Server --no-build -- --name ats1
start "EqZero gas1"  cmd /k dotnet run --project src/Server --no-build -- --name gas1
timeout /t 2 /nobreak >nul
start "EqZero gate1" cmd /k dotnet run --project src/Server --no-build -- --name gate1
start "EqZero gate2" cmd /k dotnet run --project src/Server --no-build -- --name gate2
timeout /t 1 /nobreak >nul
start "EqZero login" cmd /k dotnet run --project src/Server --no-build -- --name login

echo [server.bat] launched 6 windows.
endlocal
