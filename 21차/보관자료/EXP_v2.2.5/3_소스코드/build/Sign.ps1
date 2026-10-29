param([Parameter(Mandatory=$true)][string]$SignTool, [Parameter(Mandatory=$true)][string]$File, [Parameter(Mandatory=$true)][string]$CertificateThumbprint, [Parameter(Mandatory=$true)][string]$TimestampUrl)
$ErrorActionPreference = 'Stop'
if ($TimestampUrl -notmatch '^https://') { throw 'An HTTPS RFC3161 timestamp endpoint is required.' }
& $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $File
if ($LASTEXITCODE -ne 0) { throw 'Authenticode signing failed.' }
& $SignTool verify /pa $File
if ($LASTEXITCODE -ne 0) { throw 'Authenticode verification failed.' }
