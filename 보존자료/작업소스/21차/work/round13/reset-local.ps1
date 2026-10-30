$ErrorActionPreference='Stop'
$dataRoot=[IO.Path]::GetFullPath((Join-Path $env:APPDATA 'BunjangAuto'))
$backupRoot=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'backup-path.txt') -Raw).Trim()
if (-not $backupRoot.StartsWith($PSScriptRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected backup path' }
$snapshot=Join-Path $backupRoot ('local-reset-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $snapshot | Out-Null
# Close only the application's dedicated driver/profile processes.
Get-CimInstance Win32_Process | Where-Object {
    ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($dataRoot+'\drivers\',[StringComparison]::OrdinalIgnoreCase)) -or
    ($_.Name -eq 'chrome.exe' -and $_.CommandLine -like ('*'+$dataRoot+'\chrome-profile*'))
} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
$preserved=@('products.json','photos','categories.json','screenshots','diag-20261006-112226')
$before=@{}
foreach($name in $preserved){
 $target=Join-Path $dataRoot $name
 if(Test-Path -LiteralPath $target){
  Copy-Item -LiteralPath $target -Destination $snapshot -Recurse
  Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
 }
}
$resetNames=@('license.key','license.clock.v3','license.seen','usage.v1','usage.v1.tmp','usage.dat','terms.ok','settings.json','selectors.json','client.id','telemetry.json','telemetry.token','telemetry-queue.jsonl','chrome-profile','logs')
foreach($name in $resetNames){
 $target=[IO.Path]::GetFullPath((Join-Path $dataRoot $name))
 if(-not $target.StartsWith($dataRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected reset target'}
 if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $snapshot $name)}
}
foreach($regPath in @('HKCU:\Software\EXP\Usage','HKCU:\Software\Lumivolt\Issuer')){
 if(Test-Path -LiteralPath $regPath){
  Get-ItemProperty -LiteralPath $regPath | Export-Clixml -LiteralPath (Join-Path $snapshot (($regPath -replace '[\\:]','_')+'.xml'))
  Remove-Item -LiteralPath $regPath -Recurse
 }
}
$oldVault='C:\Users\PUVKID\Documents\Lumivolt_AdminVault_20261013'
if(Test-Path -LiteralPath $oldVault){
 if((Resolve-Path -LiteralPath $oldVault).Path -ne $oldVault){throw 'Unexpected old vault'}
 Move-Item -LiteralPath $oldVault -Destination (Join-Path $backupRoot 'old-admin-vault')
}
$provider=[Security.Cryptography.CngProvider]::new('Microsoft Platform Crypto Provider')
foreach($keyName in @('Lumivolt.Device.v3','EXP.Usage.v1')){
 if([Security.Cryptography.CngKey]::Exists($keyName,$provider)){
  $key=[Security.Cryptography.CngKey]::Open($keyName,$provider)
  try{$key.Delete()}finally{$key.Dispose()}
 }
 if([Security.Cryptography.CngKey]::Exists($keyName,$provider)){throw "Key remains: $keyName"}
}
foreach($file in $before.Keys){if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $before[$file]){throw 'Preserved file changed'}}
foreach($name in $resetNames){if(Test-Path -LiteralPath (Join-Path $dataRoot $name)){throw "Reset file remains: $name"}}
$report=@("Reset completed: $(Get-Date -Format o)","Preserved files hash verified: $($before.Count)",'Client authentication, trial/usage state, dedicated browser and settings cleared.','Master settings and old vault archived. New 13th issuer retained.','Both application-specific TPM credentials absent.','Product file was already empty before reset; original source recovery pending.')
$report | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'reset-results.txt')
$report
