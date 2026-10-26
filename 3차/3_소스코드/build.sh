#!/bin/bash
# Mono 컴파일러(mcs)로 .NET Framework 4.7.2 용 단일 exe 를 만든다. (Windows 에서는 build.bat 사용)
set -e
cd "$(dirname "$0")"
L=lib
REFS=""
RES=""
for f in $L/*.dll; do
  n=$(basename "$f" .dll)
  REFS="$REFS -r:$f"
  RES="$RES -resource:$f,lib.$n.dll"
done
mkdir -p dist
mcs -target:winexe -sdk:4.7.2 -optimize+ -langversion:latest -nowarn:1701,1702,0618,0168,0219,0414 \
  -out:dist/BunjangAuto.exe \
  $REFS $RES \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Core.dll -r:Microsoft.CSharp.dll -r:System.Management.dll \
  src/*.cs
ls -la dist/BunjangAuto.exe
