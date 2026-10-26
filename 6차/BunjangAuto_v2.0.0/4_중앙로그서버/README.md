# BunjangAuto 중앙 로그 서버 (v2.0)

사용자 PC 의 BunjangAuto.exe 가 보내는 운영 로그·시스템 정보를 받아 저장하고, 관리자가 웹 대시보드(또는 EXE 의 관리자 센터)에서 보게 해 주는 서버입니다.
**Python 3.8+ 표준 라이브러리만** 사용합니다(설치할 패키지 없음). DB 는 SQLite 파일 하나(`data/bja_logs.db`).

```
BunjangAuto.exe → 로컬 로그 → 대기열(디스크 보관) → HTTPS(배치, 백그라운드) → 이 서버 → SQLite → 관리자 대시보드
```

## 1. 실행 (처음 한 번)
```bash
cd 4_중앙로그서버
python3 server.py init-admin --username admin      # 관리자 비밀번호(12자 이상) 입력 → DB 에 PBKDF2 해시로만 저장
python3 server.py serve --port 8080                # 처음 실행하면 data/ingest_keys.txt 에 '클라이언트 등록 키'가 생성됨
```
- 브라우저에서 `http://서버주소:8080/` → 관리자 로그인 → **Dashboard / Users / Live Logs / Errors / Statistics**
- 서버를 켜 둘 PC/VPS 가 필요합니다. 로컬 테스트는 `http://127.0.0.1:8080` 로 합니다.

## 2. HTTPS (운영 환경 필수)
- 방법 A (권장): Caddy/nginx 같은 리버스 프록시가 HTTPS 를 처리 → `Caddyfile.example` 참고. 프록시 뒤에서는 `BJA_TRUST_PROXY=1`.
- 방법 B: 이 서버가 직접 TLS 처리 → `python3 server.py serve --port 8443 --cert fullchain.pem --key privkey.pem`
- EXE 는 `https://` 주소만 받아들입니다(개발용 `http://127.0.0.1` 만 예외).
- Docker: `docker compose up -d --build` (관리자 생성: `docker compose run --rm bja-logs python server.py init-admin --username admin`)

## 3. EXE 와 연결
- **빌드할 때 넣기**: `BJA_SERVER_URL=https://logs.example.com BJA_INGEST_KEY=<data/ingest_keys.txt 의 키> ./build.sh` (Windows: `set` 후 `build.bat`)
- 또는 사용자 PC 의 `%AppData%\BunjangAuto\telemetry.json` 의 `ServerUrl`, `IngestKey` 를 직접 수정.
- 등록 키(ingest key)는 EXE 안에 들어가므로 **비밀이 아닙니다**(스팸 방지용 문턱). 관리자 비밀번호와는 완전히 별개이며 EXE 에는 관리자 정보가 전혀 없습니다.
  등록하면 서버가 설치별 토큰을 발급하고, 이후 전송은 그 토큰으로만 받습니다(토큰은 DB 에 SHA-256 으로만 저장).

## 4. 관리자 인증
- 역할은 `USER` / `ADMIN`. **ADMIN 만** 사용자/로그/통계 API 에 접근합니다(`/api/v1/admin/*`).
- 비밀번호는 서버에서만 검증(PBKDF2-HMAC-SHA256 20만 회), 로그인 5회 실패 시 IP 당 10분 잠금, 세션 8시간.
- EXE 의 **설정 → 관리자 로그인** 에서 같은 계정으로 로그인하면 사이드바에 **관리자 센터**가 나타납니다(토큰은 메모리에만 보관).

## 5. API 요약
| 메서드 | 경로 | 인증 | 설명 |
|---|---|---|---|
| POST | `/api/v1/register` | `X-Ingest-Key` | 설치 등록 → 토큰 발급 |
| POST | `/api/v1/ingest` | `X-Client-Id` + `X-Client-Token` | 로그 묶음(최대 500건) 전송, 이벤트 ID 로 중복 방지 |
| POST | `/api/v1/admin/login` | 아이디/비밀번호 | 관리자 세션 토큰 |
| GET | `/api/v1/admin/overview` `users` `users/<id>` `logs` `errors` `stats` | Bearer | 조회 |
| GET | `/api/v1/admin/logs?format=csv\|json&download=1&<필터>` | Bearer | 필터가 적용된 결과 내보내기 (CSV 는 엑셀 수식 주입 방어) |

로그 필터: `client_id, level(쉼표 구분), module, action, result, q, from, to, since_id, limit`

## 6. DB 스키마
`schema.sql` — `clients`(설치 정보), `events`(로그), `admins`, `sessions`, `login_attempts`. 이벤트는 `uid` 로 중복 저장되지 않으며, `ts/client_id/level/action` 에 인덱스가 있습니다.

## 7. 수집 항목과 제외 항목
- 수집: 운영 로그, 오류/예외 형식, Client ID(랜덤 UUID), 프로그램/Windows 버전, CPU·RAM·GPU·화면 해상도·DPI, 라이선스 이름·만료일·PC 고유번호, 기능 사용 이벤트
- 전송 전 자동 제거(Redactor): 비밀번호, 쿠키/Set-Cookie/Authorization, Bearer·JWT·세션·OAuth 토큰, 개인키 블록, 라이선스 키, 카드번호(Luhn)·계좌번호 형태, 이메일, Windows 사용자 폴더명
- 이용약관 제7조에 수집 항목이 명시되어 있습니다(앱 약관 버전이 올라가 모든 사용자가 다시 동의합니다).

## 8. 테스트
```bash
python3 tests/test_server.py                       # 8개 (인증, 중복 방지, 필터, CSV 주입 방어, 통계, 속도 제한 ...)
python3 tests/seed_demo.py http://127.0.0.1:8080 <ingest-key>       # 데모 데이터
node tests/dashboard_smoke.js http://127.0.0.1:8080                 # (npm i jsdom) 대시보드 화면 동작 점검
```
## 9. 운영 메모
- 백업: `data/` 폴더(SQLite 파일 + `ingest_keys.txt`)를 복사하면 됩니다.
- 이 서버는 소규모(수백~수천 설치) 용입니다. 더 커지면 PostgreSQL 로 옮기는 것을 권장합니다(스키마는 단순).
- 오래된 로그 정리 예: `sqlite3 data/bja_logs.db "DELETE FROM events WHERE ts < date('now','-90 days'); VACUUM;"`
