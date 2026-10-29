param([ValidateSet('All','App','Admin','Tests','Reset','BatchRepeat')][string]$Target = 'All', [string]$PublicKeyFile)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { throw 'Windows .NET Framework 4.7.2 or later is required.' }
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
if ($PublicKeyFile) {
    $key = Get-Content -LiteralPath $PublicKeyFile -Raw | ConvertFrom-Json
    $bytes = [Convert]::FromBase64String($key.publicKey)
    if ($bytes.Length -ne 32) { throw 'Expected 32-byte Ed25519 public key.' }
    $hash = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).Replace('-','').Substring(0,32)
    if ($hash -ne $key.keyId) { throw 'Key ID mismatch.' }
    $code = 'using System.Collections.Generic; namespace BunjangAuto { internal static class TrustedKeys { internal static readonly IDictionary<string, byte[]> All = new Dictionary<string, byte[]> { { "' + $hash + '", System.Convert.FromBase64String("' + $key.publicKey + '") } }; } }'
    [IO.File]::WriteAllText((Join-Path $root 'src\Security\TrustedKeys.cs'), $code, [Text.UTF8Encoding]::new($true))
}
$refs = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Security.dll','/r:System.Core.dll','/r:Microsoft.CSharp.dll','/r:System.Management.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$libs = @(Get-ChildItem lib\*.dll)
$base = @('/nologo','/optimize+','/platform:anycpu','/codepage:65001','/nowarn:1701,1702,0618,0168,0219,0414,0649,0169,0108') + $refs
$shared = @(Get-ChildItem shared\*.cs | ForEach-Object { $_.FullName })
$app = @(Get-ChildItem src\*.cs -Recurse | ForEach-Object { $_.FullName })
function Compile([string]$Name, [string[]]$Sources, [string[]]$Extra, [bool]$AllLibraries) {
    $output = Join-Path $dist ($Name + '.exe')
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
    $compilerArgs = $base + @('/out:' + $output) + $Extra
    foreach ($dll in $libs) {
        if ($AllLibraries -or $dll.Name -in @('Newtonsoft.Json.dll','BouncyCastle.Cryptography.dll')) { $compilerArgs += '/r:' + $dll.FullName; $compilerArgs += '/resource:' + $dll.FullName + ',lib.' + $dll.Name }
    }
    $compilerArgs += '/resource:resources\fonts\NanumGothicBold.ttf,font.NanumGothicBold.ttf'
    $compilerArgs += '/resource:resources\app.ico,brand.app.ico'
    $compilerArgs += '/resource:resources\brand\exp-prism-sheet.png,brand.exp-prism-sheet.png'
    $compilerArgs += '/resource:resources\brand\exp-prism-still.png,brand.exp-prism-still.png'
    foreach ($h in Get-ChildItem resources\help\*.md) { $compilerArgs += '/resource:' + $h.FullName + ',help.' + $h.Name }
    & $csc @compilerArgs @Sources
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $output)) { throw "Build failed: $Name" }
}
if ($Target -in @('All','Admin')) { Compile 'EXP-Master' ($shared + @('admin\LicenseAdmin.cs','admin\AssemblyInfo.cs','admin\IssuerService.cs','admin\CreditIssuer.cs','admin\TrialIssuer.cs','src\Theme.cs','src\Brand.cs','src\Design.cs','src\Security\TrustedKeys.cs','admin\AdminWorkflow.cs')) @('/target:winexe','/win32icon:resources\app.ico','/win32manifest:resources\app.manifest') $false }
if ($Target -in @('All','App')) {
    if (!(Test-Path src\Security\TrustedKeys.cs)) { throw 'Provide -PublicKeyFile exported by LicenseAdmin. No default or test signing key is accepted.' }
    Compile 'EXP' ($app + $shared) @('/target:winexe','/win32icon:resources\app.ico','/win32manifest:resources\app.manifest') $true
}
if ($Target -eq 'Tests') {
    Compile 'RepeatDesignUiTests' ($app + $shared + @('tests\RepeatDesignUiTests.cs')) @('/target:exe','/define:TESTBUILD','/main:RepeatDesignUiTests') $true
    Compile 'RedesignUiTests' ($app + $shared + @('tests\RedesignUiTests.cs')) @('/target:exe','/define:TESTBUILD','/main:RedesignUiTests') $true
    Compile 'ShippingFormTests' ($app + $shared + @('tests\ShippingFormTests.cs')) @('/target:exe','/define:TESTBUILD','/main:ShippingFormTests') $true
    Compile 'DpiLayoutTests' ($app + $shared + @('tests\DpiLayoutTests.cs')) @('/target:exe','/define:TESTBUILD','/main:DpiLayoutTests') $true
    Compile 'FreshInstallTests' ($app + $shared + @('admin\IssuerService.cs','tests\FreshInstallTests.cs')) @('/target:exe','/define:TESTBUILD','/main:FreshInstallTests') $true
    Compile 'ResetTests' ($app + $shared + @('admin\IssuerService.cs','tools\ResetProgram.cs','tests\ResetTests.cs')) @('/target:exe','/define:TESTBUILD','/main:ResetTests') $true
    Compile 'SurfaceUiTests' ($app + $shared + @('tests\SurfaceUiTests.cs')) @('/target:exe','/define:TESTBUILD','/main:SurfaceUiTests') $true
    Compile 'UsageRecoveryTests' ($app + $shared + @('tests\UsageRecoveryTests.cs')) @('/target:exe','/define:TESTBUILD','/main:UsageRecoveryTests') $true
    Compile 'RevisionUi' ($app + $shared + @('tests\RevisionUi.cs')) @('/target:exe','/define:TESTBUILD','/main:RevisionUi') $true
    Compile 'ReleaseTests' ($app + $shared + @('admin\IssuerService.cs','admin\AdminWorkflow.cs','admin\CreditIssuer.cs','admin\TrialIssuer.cs','tests\ReleaseTests.cs')) @('/target:exe','/define:TESTBUILD','/main:ReleaseTests') $true
    Compile 'SecurityTests' ($shared + @('tests\SecurityTests.cs','src\Security\TpmDevice.cs','admin\IssuerService.cs')) @('/target:exe','/define:TESTBUILD') $false
    Compile 'RegressionTests' ($app + $shared) @('/target:exe','/define:TESTBUILD') $true
    Compile 'UiSmoke' ($app + $shared + @('tests\UiSmoke.cs')) @('/target:exe','/define:TESTBUILD','/main:UiSmoke') $true
    Compile 'GateIntegration' ($app + $shared + @('tests\GateIntegration.cs')) @('/target:exe','/define:TESTBUILD','/main:GateIntegration') $true
    Compile 'AdminUiSmoke' ($shared + @('admin\LicenseAdmin.cs','admin\AssemblyInfo.cs','admin\IssuerService.cs','src\Theme.cs','src\Brand.cs','src\Design.cs','src\Security\TrustedKeys.cs','admin\AdminWorkflow.cs','admin\CreditIssuer.cs','admin\TrialIssuer.cs','tests\AdminUiSmoke.cs')) @('/target:exe','/main:AdminUiSmoke') $false
}
if ($Target -in @('Tests','BatchRepeat')) {
    $batchSources = @('src\Models.cs','src\Schedule.cs','src\BatchRepeatSchedule.cs','src\Runner.cs','src\UI\AutomationController.cs','src\Security\UsageQuota.cs','tests\BatchRepeatTests.cs')
    Compile 'BatchRepeatTests' ($batchSources + $shared) @('/target:exe','/define:TESTBUILD,BATCHREPEATTESTS','/main:BatchRepeatTests') $false
}
Write-Host "Build completed: $Target -> $dist"



if ($Target -eq 'Reset') { Compile 'EXP-Reset' (@($app | Where-Object { [IO.Path]::GetFileName($_) -ne 'AssemblyInfo.cs' }) + $shared + @('admin\IssuerService.cs','tools\ResetProgram.cs','tools\ResetAssemblyInfo.cs')) @('/target:winexe','/main:ExpResetTool.ResetProgram','/win32icon:resources\app.ico','/win32manifest:resources\app.manifest') $true }
