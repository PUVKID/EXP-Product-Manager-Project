@echo off
rem ============================================================================
rem  Lumivolt 2.0  -  Windows 에서 직접 빌드 (Visual Studio / .NET SDK 불필요)
rem  Windows 10/11 에 기본 포함된 .NET Framework 4.x 의 csc.exe 를 사용합니다.
rem
rem  중앙 로그 서버 주소/등록 키를 exe 안에 넣고 싶으면 빌드 전에 지정하세요:
rem      set BJA_SERVER_URL=https://logs.example.com
rem      set BJA_INGEST_KEY=서버의 data\ingest_keys.txt 에 있는 키
rem      build.bat
rem  (지정하지 않으면 비워 두고, 사용자 PC 의 %%AppData%%\BunjangAuto\telemetry.json 에서 설정)
rem ============================================================================
setlocal enabledelayedexpansion
chcp 65001 >nul
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo csc.exe 를 찾을 수 없습니다: %CSC% & goto :end)

rem --- 서버 주소/키 주입
> src\Telemetry\BuildConfig.cs echo namespace BunjangAuto { public static class BuildConfig { public const string ServerUrl = "%BJA_SERVER_URL%"; public const string IngestKey = "%BJA_INGEST_KEY%"; } }

set REFS=
set RES=
for %%f in (lib\*.dll) do (
  set REFS=!REFS! /r:%%f
  set RES=!RES! /resource:%%f,lib.%%~nf.dll
)
if exist resources\fonts\NanumGothicBold.ttf set RES=!RES! /resource:resources\fonts\NanumGothicBold.ttf,font.NanumGothicBold.ttf
for %%f in (resources\help\*.md) do (
  set RES=!RES! /resource:%%f,help.%%~nxf
)
set SRC=
for /r src %%f in (*.cs) do set SRC=!SRC! "%%f"
if not exist dist mkdir dist
"%CSC%" /target:winexe /optimize+ /platform:anycpu /nowarn:1701,1702,0618,0168,0219,0414,0649,0169,0108 ^
  /win32icon:resources\app.ico /win32manifest:resources\app.manifest ^
  /out:dist\Lumivolt.exe !REFS! !RES! ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Security.dll ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Core.dll /r:Microsoft.CSharp.dll /r:System.Management.dll ^
  !SRC!
echo.
if exist dist\Lumivolt.exe (echo 빌드 성공: dist\Lumivolt.exe) else (echo 빌드 실패)
:end
pause
