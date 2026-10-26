#!/bin/bash
# BunjangAuto 2.0 회귀 테스트 (Linux/Mono 에서 자동으로 확인할 수 있는 부분)
#   사용:  tests/regression.sh [원본_ZIP]        (원본 ZIP 을 주면 '구버전 데이터 → 신버전' 마이그레이션 테스트도 실행)
# Windows 에서만 확인할 수 있는 항목(Chrome/Selenium 실제 등록, DWM 창 꾸밈, 125~200% DPI)은 tests/WINDOWS_CHECKLIST.md 를 따라 직접 확인한다.
set -u
cd "$(dirname "$0")/.."
ROOT=$(pwd); SRV="$ROOT/../4_중앙로그서버"; export LANG=C.UTF-8 LC_ALL=C.UTF-8
PASS=0; FAIL=0
step() { echo; echo "=== $1"; }
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
bad()  { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

step "1. 빌드 (mcs, .NET Framework 4.7.2)"
if bash build.sh >/tmp/bja_build.log 2>&1 && [ -f dist/BunjangAuto.exe ]; then ok "BunjangAuto.exe 빌드"; else bad "빌드 실패 (/tmp/bja_build.log)"; fi
if bash build_admin.sh >/tmp/bja_build_admin.log 2>&1 && [ -f dist/LicenseAdmin.exe ]; then ok "LicenseAdmin.exe 빌드"; else bad "관리자 발급툴 빌드 실패"; fi

step "2. SelfTest (기존 1~6 + v2 추가 검사)"
H=$(mktemp -d); OUT=$(cd dist && HOME=$H XDG_CONFIG_HOME=$H/.config timeout 300 mono BunjangAuto.exe --selftest 2>&1)
echo "$OUT" | grep -E "^\s*\[?(PASS|FAIL)" | sed 's/^/  /' | grep -c PASS | xargs -I{} echo "  통과 항목: {}"
if echo "$OUT" | grep -q "ALL PASSED"; then ok "SelfTest ALL PASSED"; else bad "SelfTest 실패"; echo "$OUT" | grep FAIL; fi

step "3. 중앙 로그 서버 단위/통합 테스트 (8개)"
if (cd "$SRV" && python3 tests/test_server.py >/tmp/bja_srv_test.log 2>&1); then ok "서버 테스트 OK"; else bad "서버 테스트 실패 (/tmp/bja_srv_test.log)"; fi

step "4. 종단 테스트: EXE 의 전송 계층 → 실제 서버 → 관리자 API"
D=$(mktemp -d); export BJA_DATA_DIR=$D BJA_INGEST_KEYS=reg-key BJA_QUIET=1
(cd "$SRV" && BJA_ADMIN_PASSWORD='regression-pass-123' python3 server.py init-admin --username admin >/dev/null)
(cd "$SRV" && python3 server.py serve --host 127.0.0.1 --port 18555 >/tmp/bja_srv.log 2>&1) & SP=$!
sleep 1.5
R=$(cd dist && HOME=$H mono BunjangAuto.exe --selftest-central http://127.0.0.1:18555 reg-key 2>&1 | grep -E "SENT|FAILED")
echo "  $R"
TOK=$(curl -s -X POST localhost:18555/api/v1/admin/login -d '{"username":"admin","password":"regression-pass-123"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])")
LOGS=$(curl -s -H "Authorization: Bearer $TOK" "localhost:18555/api/v1/admin/logs?limit=20")
echo "$R" | grep -q SENT && ok "EXE → 서버 전송" || bad "EXE → 서버 전송"
echo "$LOGS" | grep -q "REDACTED" && ! echo "$LOGS" | grep -q "SECRET-VALUE-1" && ok "서버에 저장된 로그에서 비밀번호 값이 제거됨" || bad "비밀정보 제거 확인 실패"
curl -s -H "Authorization: Bearer $TOK" "localhost:18555/api/v1/admin/logs?format=csv&download=1" | head -c 300 | grep -q "client_id" && ok "관리자 CSV 내보내기" || bad "CSV 내보내기"
curl -s -o /dev/null -w "%{http_code}" localhost:18555/api/v1/admin/users | grep -q 401 && ok "관리자 API 는 로그인 없이 401" || bad "관리자 API 보호"
if command -v node >/dev/null && [ -d /tmp/jt/node_modules/jsdom ]; then
  python3 "$SRV/tests/seed_demo.py" http://127.0.0.1:18555 reg-key >/dev/null
  if (cd /tmp/jt && NODE_PATH=/tmp/jt/node_modules BJA_ADMIN_PASSWORD='regression-pass-123' node "$SRV/tests/dashboard_smoke.js" http://127.0.0.1:18555 2>&1 | grep -q "JS errors: \[\]"); then ok "관리자 웹 대시보드 렌더링(jsdom) 오류 없음"; else bad "대시보드 스모크 테스트"; fi
else echo "  (건너뜀) 대시보드 스모크 테스트: npm i jsdom 후 /tmp/jt 에서 실행"; fi
kill $SP 2>/dev/null

if [ -n "${1:-}" ] && [ -f "$1" ]; then
  step "5. 마이그레이션: 구버전(v1)이 만든 암호화 settings/products → 신버전(v2)"
  W=$(mktemp -d); mkdir -p $W/old $W/new $W/o; (cd $W/o && unzip -q "$1")
  (cd $W/o/3_소스코드 && bash build.sh >/dev/null 2>&1)
  cp $W/o/3_소스코드/dist/BunjangAuto.exe $W/old/; cp "$ROOT/dist/BunjangAuto.exe" $W/new/; cp lib/*.dll $W/old/; cp lib/*.dll $W/new/
  REFS="-r:Newtonsoft.Json.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll"
  mcs -out:$W/old/w.exe tests/migration/write_with_old.cs -lib:$W/old -r:$W/old/BunjangAuto.exe $REFS 2>/dev/null
  mcs -out:$W/new/r.exe tests/migration/read_with_new.cs -lib:$W/new -r:$W/new/BunjangAuto.exe $REFS 2>/dev/null
  M=$(mktemp -d); (cd $W/old && HOME=$M mono w.exe >/dev/null 2>&1); RES=$(cd $W/new && HOME=$M mono r.exe 2>&1 | grep -E "migrated|persisted")
  echo "$RES" | sed 's/^/  /'
  echo "$RES" | grep -q "settings migrated: True  products migrated: True" && ok "구버전 데이터 모든 필드 보존" || bad "마이그레이션"
fi
echo; echo "결과: PASS $PASS / FAIL $FAIL"; [ $FAIL -eq 0 ]
