param([string]$ISCC)
$ErrorActionPreference='Stop'
if (!$ISCC) {
    $candidates=@((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe'))
    $ISCC=$candidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
}
if (!$ISCC -or !(Test-Path -LiteralPath $ISCC)) { throw 'Install Inno Setup, or supply -ISCC with the full path to ISCC.exe.' }
& (Join-Path $PSScriptRoot 'Build.ps1') -Target All
& $ISCC (Join-Path (Split-Path $PSScriptRoot -Parent) 'installer/EXP.iss')
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
