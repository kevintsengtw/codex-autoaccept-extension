@echo off
setlocal
"%~dp0CodexUiaProbe.exe" --auto-accept --max-depth 60 %*
set "probe_exit=%errorlevel%"
echo.
echo Auto mode exit code: %probe_exit%
pause
exit /b %probe_exit%
