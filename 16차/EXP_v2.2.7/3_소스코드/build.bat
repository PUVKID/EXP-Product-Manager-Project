@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\Build.ps1" %*
exit /b %errorlevel%
