@echo off
rem IExpress can only launch a command line, so this is the doorstep: it hands
rem over to install.ps1, which does the work and asks for administrator rights
rem if it needs them.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 (
    echo.
    echo Installation did not finish.
    pause
)
