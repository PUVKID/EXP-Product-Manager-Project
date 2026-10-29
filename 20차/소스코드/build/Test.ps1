param([string]$LocalLicense, [string]$OtherDeviceLicense)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -Target Tests
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'dist\SecurityTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Security tests failed.' }
& (Join-Path $root 'dist\RegressionTests.exe') --selftest
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
& (Join-Path $root 'dist\UiSmoke.exe') (Join-Path $root 'dist\ui-tests')
if ($LASTEXITCODE -ne 0) { throw 'UI render tests failed.' }
if ($LocalLicense -and $OtherDeviceLicense) {
    & (Join-Path $root 'dist\GateIntegration.exe') $LocalLicense $OtherDeviceLicense
    if ($LASTEXITCODE -ne 0) { throw 'Gate integration tests failed.' }
}
