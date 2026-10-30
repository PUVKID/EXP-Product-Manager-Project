param([string]$Phase = 'red', [string]$TestCase = '')
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Join-Path $project '소스코드'
Set-Location -LiteralPath $source
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $source 'dist\BatchRepeatTests.exe'
New-Item -ItemType Directory -Force (Split-Path $output -Parent) | Out-Null
$arguments = @('/nologo','/optimize+','/codepage:65001','/target:exe','/define:TESTBUILD','/main:BatchRepeatTests',('/out:'+$output),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Core.dll','/r:System.Security.dll','/r:System.Numerics.dll')
foreach ($name in @('Newtonsoft.Json.dll','BouncyCastle.Cryptography.dll')) {
 $path = Join-Path $source ('lib\'+$name)
 $arguments += '/r:'+$path
 $arguments += '/resource:'+$path+',lib.'+$name
}
$arguments += @('src\Models.cs','src\Schedule.cs','src\Runner.cs','src\UI\AutomationController.cs','src\Security\UsageQuota.cs','shared\EditionPolicy.cs','shared\LicenseProtocol.cs','shared\CreditProtocol.cs','shared\LicenseText.cs','tests\BatchRepeatTests.cs')
if ($Phase -like 'baseline-red*') { $arguments = @($arguments | Where-Object { $_ -notin @('src\Models.cs','src\Runner.cs','src\UI\AutomationController.cs') }) + @((Join-Path $PSScriptRoot 'baseline\Models.cs'),(Join-Path $PSScriptRoot 'baseline\Runner.cs'),(Join-Path $PSScriptRoot 'baseline\AutomationController.cs')) }
if ($Phase -notlike 'baseline-red*' -and (Test-Path -LiteralPath 'src\BatchRepeatSchedule.cs')) { $arguments += 'src\BatchRepeatSchedule.cs' }
$state = Get-Content -LiteralPath (Join-Path $project '.codex\routing-state\af1d78b1ccefc06781a04ef2.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$record = [ordered]@{ korean_time=(Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'); session=$state.session_id; task=$state.task_id; cwd=$source; argv=@($csc)+$arguments; phase=$Phase; repair_used=$state.repair_used; input_fix_used=$state.input_fix_used; implementation_changed=($Phase -ne 'red'); controlled_tdd_red=($Phase -eq 'red' -or $Phase -like 'baseline-red*') }
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-compile-request.json')) -Encoding utf8
& $csc @arguments *> (Join-Path $PSScriptRoot ($Phase+'-compile.txt'))
$compileCode = $LASTEXITCODE
$record.exit_code=$compileCode
$record.error=Get-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-compile.txt')) -Raw -Encoding UTF8
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-compile-result.json')) -Encoding utf8
Get-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-compile.txt')) -Encoding UTF8
if ($compileCode -ne 0) { exit $compileCode }
$testArguments=if($TestCase){@($TestCase)}else{@()}
$record.argv=@($output)+$testArguments
& $output @testArguments *> (Join-Path $PSScriptRoot ($Phase+'-tests.txt'))
$testCode=$LASTEXITCODE
$record.exit_code=$testCode
$record.error=Get-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-tests.txt')) -Raw -Encoding UTF8
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-tests-result.json')) -Encoding utf8
Get-Content -LiteralPath (Join-Path $PSScriptRoot ($Phase+'-tests.txt')) -Encoding UTF8
exit $testCode



