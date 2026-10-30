$ErrorActionPreference='Stop'
$workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$stage=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'delivery\EXP_v2.2.6')).Path
$out=Join-Path $workspace 'outputs'
$backupRoot=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'backup-path.txt') -Raw).Trim()
$archive=Join-Path $backupRoot ('prior-distribution-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $archive | Out-Null
Compress-Archive -LiteralPath $stage -DestinationPath (Join-Path $out 'EXP_v2.2.6_13차_전체.zip')
Compress-Archive -Path (Join-Path $stage '1_고객배포\*') -DestinationPath (Join-Path $out 'EXP_v2.2.6_고객배포.zip')
foreach($label in @('13차','최종')){
 $dest=[IO.Path]::GetFullPath(('C:\Users\PUVKID\Desktop\번장\'+$label))
 if($dest -notin @('C:\Users\PUVKID\Desktop\번장\13차','C:\Users\PUVKID\Desktop\번장\최종')){throw 'Unexpected destination'}
 $old=Join-Path $archive $label
 New-Item -ItemType Directory -Path $old | Out-Null
 foreach($name in @('EXP_v2.2.5','EXP_v2.2.5_고객배포','EXP_v2.2.5_13차_전체.zip','EXP_v2.2.5_고객배포.zip','EXP_Setup_2.2.5.exe','EXP.exe','EXP-Master.exe','EXP_13차_먼저읽기.txt')){
  $target=[IO.Path]::GetFullPath((Join-Path $dest $name))
  if(-not $target.StartsWith($dest+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected old package path'}
  if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination $old}
 }
 Copy-Item -LiteralPath $stage -Destination $dest -Recurse
 Copy-Item -LiteralPath (Join-Path $out 'EXP_Setup_2.2.6.exe'),(Join-Path $out 'EXP_v2.2.6_13차_전체.zip'),(Join-Path $out 'EXP_v2.2.6_고객배포.zip'),(Join-Path $out 'EXP_13차_먼저읽기.txt') -Destination $dest
 Copy-Item -LiteralPath (Join-Path $out 'EXP-Master_13차.exe') -Destination (Join-Path $dest 'EXP-Master.exe')
}

'Prepared installer packages only; no installation or activation performed.'
