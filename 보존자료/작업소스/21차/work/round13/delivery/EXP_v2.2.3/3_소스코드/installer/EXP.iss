#define AppVersion "2.2.3"
[Setup]
AppId={{BBD63563-5422-4DA7-93C6-2F0D98E87E3F}
AppName={code:ProductName}
AppVersion={#AppVersion}
AppPublisher=EXP
AppSupportURL=mailto:expmasterxx00@gmail.com
DefaultDirName={localappdata}\Programs\EXP
DefaultGroupName=EXP
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir=..\dist\installer
OutputBaseFilename=EXP_Setup_{#AppVersion}
SetupIconFile=..\resources\app.ico
UninstallDisplayIcon={app}\{code:ExecutableName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=no
RestartApplications=no
AppMutex=BunjangAuto_SingleInstance
VersionInfoVersion=2.2.3.0
VersionInfoCompany=EXP
VersionInfoDescription=EXP 통합 설치 프로그램
VersionInfoProductName=EXP
UninstallDisplayName={code:ProductName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로 가기 만들기"; GroupDescription: "바로 가기"

[Files]
Source: "..\resources\fonts\NanumGothicBold.ttf"; Flags: dontcopy
Source: "..\dist\EXP.exe"; Flags: dontcopy
Source: "..\dist\EXP.exe"; DestDir: "{app}"; DestName: "{code:ExecutableName}"; Flags: ignoreversion
Source: "..\resources\fonts\NanumGothic-OFL.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\licenses\BouncyCastle-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "..\licenses\Inno-Setup-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\EXP Classic.exe"
Type: files; Name: "{app}\EXP Studio.exe"
Type: files; Name: "{app}\EXP Pro.exe"
Type: files; Name: "{group}\EXP Classic.lnk"
Type: files; Name: "{group}\EXP Studio.lnk"
Type: files; Name: "{group}\EXP Pro.lnk"
Type: files; Name: "{autodesktop}\EXP Classic.lnk"
Type: files; Name: "{autodesktop}\EXP Studio.lnk"
Type: files; Name: "{autodesktop}\EXP Pro.lnk"

[Icons]
Name: "{group}\{code:ProductName}"; Filename: "{app}\{code:ExecutableName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{code:ProductName}"; Filename: "{app}\{code:ExecutableName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{code:ExecutableName}"; Description: "EXP 실행"; Flags: nowait postinstall skipifsilent

[Code]
var Edition: String; AuthPage: TWizardPage; Intro: TNewStaticText;
function AddFontResourceEx(FileName: String; Flags: DWORD; Reserved: Integer): Integer;
  external 'AddFontResourceExW@gdi32.dll stdcall';
function ProductName(Param: String): String;
begin Result := 'EXP'; if Edition <> '' then Result := Result + ' ' + Edition; end;
function ExecutableName(Param: String): String;
begin Result := ProductName('') + '.exe'; end;
function Authenticate(Silent: Boolean): Boolean;
var Code: Integer; Args: String;
begin
  Result := False;
  ExtractTemporaryFile('EXP.exe');
  Args := '--activate-for-setup'; if Silent then Args := '--check-for-setup';
  if not Exec(ExpandConstant('{tmp}\EXP.exe'), Args, ExpandConstant('{tmp}'), SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then Exit;
  case Code of 10: Edition := 'Classic'; 20: Edition := 'Studio'; 30: Edition := 'Pro'; else Exit; end;
  Result := True;
end;
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 461808);
  if not Result then MsgBox('.NET Framework 4.7.2 이상이 필요합니다.', mbError, MB_OK);
end;
procedure InitializeWizard();
begin
  ExtractTemporaryFile('NanumGothicBold.ttf'); AddFontResourceEx(ExpandConstant('{tmp}\NanumGothicBold.ttf'),16,0);
  WizardForm.Font.Name := 'NanumGothic'; WizardForm.Font.Style := [fsBold];
  AuthPage := CreateCustomPage(wpWelcome,'EXP 시작하기','받은 인증키에 맞는 제품이 자동으로 설치됩니다.');
  Intro := TNewStaticText.Create(AuthPage); Intro.Parent := AuthPage.Surface;
  Intro.Left := 0; Intro.Top := ScaleY(16); Intro.Width := AuthPage.SurfaceWidth; Intro.Height := ScaleY(220); Intro.AutoSize := False; Intro.WordWrap := True;
  Intro.Caption := 'Classic · 하루 25개' + #13#10 + 'Studio · 하루 50개 + 반복·예약' + #13#10 + 'Pro · 하루 100개 + 반복·예약 + 추가 등록권' + #13#10#13#10 + '다음을 누르면 인증 화면이 열립니다.' + #13#10 + 'Pro 3시간 체험판도 같은 설치 파일을 사용합니다.' + #13#10#13#10 + '체험판 신청 및 추가 등록권 문의' + #13#10 + 'expmasterxx00@gmail.com';
end;
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = AuthPage.ID then begin
    Result := Authenticate(WizardSilent);
    if not Result and not WizardSilent then MsgBox('인증을 완료해야 설치할 수 있습니다.',mbInformation,MB_OK);
    if Result then WizardForm.Caption := ProductName('') + ' 설치';
  end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var OriginalEdition: String;
begin
  OriginalEdition := Edition; Result := '';
  if not Authenticate(True) then Result := '인증이 만료되었거나 변경되었습니다. 설치 파일을 다시 실행해 주세요.'
  else if Edition <> OriginalEdition then Result := '인증 등급이 변경되었습니다. 설치 파일을 다시 실행해 주세요.';
end;
// Customer data, TPM enrollment and usage records deliberately survive uninstall.
