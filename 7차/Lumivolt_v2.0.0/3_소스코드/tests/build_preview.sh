#!/bin/bash
# 개발 전용 빌드: UIPREVIEW 기호를 켜서 화면 점검 도구(--ui-preview / --ui-selftest / --ui-layout)가 들어간 exe 를 만든다. 배포하지 않는다.
set -e
cd "$(dirname "$0")/.."
OUTDIR=${1:-/tmp/lumivolt-preview}
REFS=""; RES=""
for f in lib/*.dll; do n=$(basename $f .dll); REFS="$REFS -r:$f"; RES="$RES -resource:$f,lib.$n.dll"; done
RES="$RES -resource:resources/fonts/NanumGothicBold.ttf,font.NanumGothicBold.ttf"
for f in resources/help/*.md; do RES="$RES -resource:$f,help.$(basename $f)"; done
mkdir -p "$OUTDIR"
mcs -target:winexe -sdk:4.7.2 -define:UIPREVIEW -langversion:latest -nowarn:1701,1702,0618,0168,0219,0414,0649,0169,0108 -out:"$OUTDIR/Preview.exe" $REFS $RES \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Core.dll -r:Microsoft.CSharp.dll -r:System.Management.dll \
  $(find src -name '*.cs' | sort)
cp lib/*.dll "$OUTDIR"/ 2>/dev/null || true
echo "$OUTDIR/Preview.exe"
