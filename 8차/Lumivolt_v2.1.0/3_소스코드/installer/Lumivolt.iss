; Deferred installer configuration. This delivery is the portable EXE ZIP.
#define AppVersion "2.1.0"
[Setup]
AppId={{BBD63563-5422-4DA7-93C6-2F0D98E87E3F}
AppName=Lumivolt
AppVersion={#AppVersion}
AppPublisher=음악천재
DefaultDirName={localappdata}\Programs\Lumivolt
DefaultGroupName=Lumivolt
DisableDirPage=no
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir=..\dist\installer
OutputBaseFilename=Lumivolt_Setup_{#AppVersion}
SetupIconFile=..\resources\app.ico
UninstallDisplayIcon={app}\Lumivolt.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
AppMutex=BunjangAuto_SingleInstance
VersionInfoVersion=2.1.0.0
VersionInfoCompany=음악천재

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로 가기 만들기"; GroupDescription: "바로 가기"; Flags: unchecked

[Files]
Source: "..\dist\Lumivolt.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\resources\fonts\NanumGothic-OFL.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\licenses\BouncyCastle-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\..\1_배포용_앱\상품양식.csv"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\Lumivolt"; Filename: "{app}\Lumivolt.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Lumivolt"; Filename: "{app}\Lumivolt.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Lumivolt.exe"; Description: "Lumivolt 실행"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 461808);
  if not Result then MsgBox('.NET Framework 4.7.2 이상을 먼저 설치해 주세요.', mbError, MB_OK);
end;
; No AppData deletion, TPM-key removal, registry-key removal, or wildcard payload copy.
