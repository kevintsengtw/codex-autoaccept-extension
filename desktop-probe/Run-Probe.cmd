@echo off
setlocal
"%~dp0CodexUiaProbe.exe" %*
set "probe_exit=%errorlevel%"
echo.
echo Probe exit code: %probe_exit%
echo Press any key to close this window.
pause >nul
exit /b %probe_exit%
