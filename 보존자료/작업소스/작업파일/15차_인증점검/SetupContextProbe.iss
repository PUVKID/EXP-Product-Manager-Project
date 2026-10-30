[Setup]
AppName=EXP Setup Context Probe
AppVersion=1
DefaultDirName={tmp}\EXP-NoInstall
PrivilegesRequired=lowest
Uninstallable=no
OutputBaseFilename=SetupContextProbe
OutputDir=.
[Files]
Source: "Audit.exe"; Flags: dontcopy
Source: "extracted-2.2.6\tmp\EXP.exe"; Flags: dontcopy
[Code]
procedure InitializeWizard();
var Code: Integer;
begin
ExtractTemporaryFile('Audit.exe'); ExtractTemporaryFile('EXP.exe');
Exec(ExpandConstant('{tmp}\Audit.exe'), '"{#SourcePath}inno-before.txt"', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code);
Exec(ExpandConstant('{tmp}\EXP.exe'), '--check-for-setup', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code);
SaveStringToFile('{#SourcePath}inno-code.txt',IntToStr(Code),False);
Exec(ExpandConstant('{tmp}\Audit.exe'), '"{#SourcePath}inno-after.txt"', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin Result := 'Diagnostic complete. Installation disabled.'; end;