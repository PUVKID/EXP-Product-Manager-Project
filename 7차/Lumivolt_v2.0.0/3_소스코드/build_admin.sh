#!/bin/bash
set -e
cd "$(dirname "$0")"
mkdir -p dist
mcs -target:winexe -sdk:4.7.2 -optimize+ -win32icon:resources/app.ico -resource:resources/fonts/NanumGothicBold.ttf,font.NanumGothicBold.ttf -out:dist/LicenseAdmin.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll admin/LicenseAdmin.cs src/License.cs src/Theme.cs
ls -la dist
