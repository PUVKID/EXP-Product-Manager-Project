$ErrorActionPreference='Stop'
$work='C:\Users\PUVKID\Desktop\번장\작업파일\2.2.7_상점확인수정'
try {
    $appRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\EXP'))
    if($appRoot -ne 'C:\Users\PUVKID\AppData\Local\Programs\EXP'){throw 'Unexpected install folder'}
    $uninstaller=Join-Path $appRoot 'unins000.exe'
    if(Test-Path -LiteralPath $uninstaller){
        $p=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -PassThru -Wait
        if($p.ExitCode -ne 0){throw ('Uninstaller exit '+$p.ExitCode)}
    }
    $remaining=@(Get-ChildItem -LiteralPath $appRoot -Filter 'EXP*.exe' -ErrorAction SilentlyContinue)
    if($remaining.Count){throw 'Installed EXP executable still exists'}
    @{success=$true;installedExecutables=0}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'uninstall-result.json') -Encoding UTF8
}catch{
    @{success=$false;message=$_.Exception.Message}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'uninstall-result.json') -Encoding UTF8
    exit 1
}
