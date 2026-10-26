#!/bin/bash
# Mono 컴파일러(mcs)로 .NET Framework 4.7.2 용 단일 exe 를 만든다. (Windows 에서는 build.bat 사용)
#   BJA_SERVER_URL=https://logs.example.com BJA_INGEST_KEY=xxxx ./build.sh   ← 중앙 로그 서버 주소/등록키를 exe 에 넣어 빌드
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
# 도움말 문서(오프라인)를 exe 안에 내장
for f in resources/help/*.md; do
  [ -f "$f" ] && RES="$RES -resource:$f,help.$(basename "$f")"
done
EXTRA=""
[ -f resources/app.ico ] && EXTRA="$EXTRA -win32icon:resources/app.ico"
# (mcs 는 -win32manifest 를 지원하지 않아 DPI 설정은 Program.EnableDpiAwareness() 가 대신한다. Windows 의 csc 빌드(build.bat)는 매니페스트를 내장한다)
DEFS=""
[ -n "$BJA_DEFINE" ] && DEFS="-define:$BJA_DEFINE"

# 서버 주소/등록키 주입 (환경변수가 없으면 비워 둔다 → telemetry.json 에서 설정)
cat > src/Telemetry/BuildConfig.cs <<CS
namespace BunjangAuto
{
    // 이 파일은 build.sh / build.bat 가 환경변수(BJA_SERVER_URL, BJA_INGEST_KEY)로 다시 생성한다.
    public static class BuildConfig
    {
        public const string ServerUrl = "${BJA_SERVER_URL:-}";
        public const string IngestKey = "${BJA_INGEST_KEY:-}";
    }
}
CS

mkdir -p dist
SRC=$(find src -name '*.cs' | sort)
mcs -target:winexe -sdk:4.7.2 -optimize+ -langversion:latest -nowarn:1701,1702,0618,0168,0219,0414,0649,0169 $DEFS $EXTRA \
  -out:dist/BunjangAuto.exe \
  $REFS $RES \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Core.dll -r:Microsoft.CSharp.dll -r:System.Management.dll \
  $SRC
ls -la dist/BunjangAuto.exe
