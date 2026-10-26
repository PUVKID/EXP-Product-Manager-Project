@echo off
rem Windows 에서 직접 빌드: .NET Framework 4.x 에 포함된 csc.exe 사용 (Visual Studio 불필요)
setlocal enabledelayedexpansion
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set REFS=
set RES=
for %%f in (lib\*.dll) do (
  set REFS=!REFS! /r:%%f
  set RES=!RES! /resource:%%f,lib.%%~nf.dll
)
if not exist dist mkdir dist
"%CSC%" /target:winexe /optimize+ /nowarn:1701,1702 /win32icon:app.ico /out:dist\BunjangAuto.exe !REFS! !RES! ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Security.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Core.dll /r:Microsoft.CSharp.dll /r:System.Management.dll ^
  src\*.cs
echo.
if exist dist\BunjangAuto.exe (echo 빌드 성공: dist\BunjangAuto.exe) else (echo 빌드 실패)
pause
