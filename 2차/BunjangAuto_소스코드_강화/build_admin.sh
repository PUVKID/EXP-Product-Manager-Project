#!/bin/bash
set -e
cd "$(dirname "$0")"
mkdir -p dist
mcs -target:winexe -sdk:4.7.2 -optimize+ -out:dist/LicenseAdmin.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll admin/LicenseAdmin.cs src/License.cs
cp keygen/private.xml dist/license_private.xml
ls -la dist
