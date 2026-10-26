param([Parameter(Mandatory=$true)][string]$Scale)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root ('work\dpi-'+$Scale)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$log=Join-Path $output 'test.log'
& (Join-Path $root '소스코드\dist\DpiLayoutTests.exe') $output $Scale *> $log
$code=$LASTEXITCODE
Get-Content -LiteralPath $log -Tail 8
exit $code
