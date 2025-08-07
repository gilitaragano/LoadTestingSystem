@echo off
setlocal

set "DIR=%~dp0bin\Debug\net8.0"
set "EXE=LoadTestingSystem.exe"

start "Run Resolve" cmd /k "cd /d %DIR% && %EXE% resolve"

endlocal