param([Parameter(Mandatory=$true)][string]$IsccPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!(Test-Path -LiteralPath (Join-Path $project 'dist\Lumivolt.exe'))) { throw 'Build the application first using Build.ps1.' }
& $IsccPath (Join-Path $project 'installer\Lumivolt.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup build failed.' }
