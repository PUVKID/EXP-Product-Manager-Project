param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$bundle = Split-Path $project -Parent
foreach ($name in @('Lumivolt.exe','LicenseAdmin.exe')) { if (!(Test-Path -LiteralPath (Join-Path $project ('dist\' + $name)))) { throw 'Run Build.ps1 first.' } }
$customer = Join-Path $bundle '1_배포용_앱'
$admin = Join-Path $bundle '2_관리자용_발급툴'
New-Item -ItemType Directory -Force $customer,$admin,$OutputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'dist\Lumivolt.exe') -Destination $customer
Copy-Item -LiteralPath (Join-Path $project 'dist\LicenseAdmin.exe') -Destination $admin
$forbidden = @(Get-ChildItem -LiteralPath $bundle -Recurse -File | Where-Object { $_.Name -match '(?i)license_private|\.dpapi$|\.lvbackup$|\.pfx$|\.p12$' })
if ($forbidden.Count -gt 0) { throw 'Secret key files detected. Do not distribute this tree.' }
foreach ($folder in @($customer,$admin)) {
    New-Item -ItemType Directory -Force (Join-Path $folder 'licenses') | Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'resources\fonts\NanumGothic-OFL.txt') -Destination (Join-Path $folder 'licenses')
    Copy-Item -LiteralPath (Join-Path $project 'licenses\BouncyCastle-LICENSE.txt') -Destination (Join-Path $folder 'licenses')
}
$zip = Join-Path $OutputDirectory 'Lumivolt_v2.1.0_Customer.zip'
if (Test-Path -LiteralPath $zip) { throw 'Output already exists; use a new output folder.' }
Compress-Archive -LiteralPath $customer -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Customer package: $zip"
$stage = Join-Path ([IO.Path]::GetTempPath()) ('Lumivolt-package-' + [Guid]::NewGuid().ToString('N'))
$stagedBundle = Join-Path $stage 'Lumivolt_v2.1.0'
New-Item -ItemType Directory -Force $stagedBundle | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $bundle -Recurse -File) {
    $relative = $file.FullName.Substring($bundle.Length + 1)
    if ($relative -match '(^|\\)(dist|bin|obj|\.git|__pycache__)(\\|$)') { continue }
    $destination = Join-Path $stagedBundle $relative
    New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
$fullZip = Join-Path $OutputDirectory 'Lumivolt_v2.1.0_Complete.zip'
if (Test-Path -LiteralPath $fullZip) { throw 'Full output already exists.' }
Compress-Archive -LiteralPath $stagedBundle -DestinationPath $fullZip -CompressionLevel Optimal
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedStage = [IO.Path]::GetFullPath($stage)
if ($resolvedStage.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and ([IO.Path]::GetFileName($resolvedStage) -match '^Lumivolt-package-[a-f0-9]{32}$')) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
Write-Host "Complete project: $fullZip"
