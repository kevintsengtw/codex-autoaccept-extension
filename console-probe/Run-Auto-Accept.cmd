@echo off
setlocal
"%~dp0CodexUiaProbe.exe" --auto-accept %*
set "probe_exit=%errorlevel%"
echo.
echo Auto mode exit code: %probe_exit%
exit /b %probe_exit%
