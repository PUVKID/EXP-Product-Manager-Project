# 번개장터 자동등록 (BunjangAuto) 2.0.0  — 음악천재

기존 v1 의 기능을 기준선으로 두고, UI 를 Windows 11 스타일로 바꾸고 관리자용 중앙 로그/통계, 내장 사용 설명서, Light/Dark/System 테마를 추가한 버전입니다.

## 먼저 알려 드릴 것 (정직한 요약)

| 질문 | 답 |
|---|---|
| **빌드가 성공했나요?** | **예.** `3_소스코드/build.sh`(Mono `mcs`)로 Release 빌드 성공, `BunjangAuto.exe`(약 9.6MB, 단일 파일)와 `LicenseAdmin.exe` 생성. |
| **EXE 경로** | `1_배포용_앱/BunjangAuto.exe` (원본 빌드 위치: `3_소스코드/dist/`) · 관리자 발급툴 `2_관리자용_발급툴/LicenseAdmin.exe` |
| **.NET 버전** | **.NET Framework 4.7.2** (Windows 10 1803+ / Windows 11 에 기본 포함 → 설치 없이 실행). **.NET 10 이 아닙니다.** |
| **UI 프레임워크** | **WinForms + 직접 그린 Fluent 컨트롤**(카드·사이드바·토글·세그먼트·차트 등). **WinUI 3/WPF 가 아닙니다.** |
| **왜 .NET 10/WPF/WinUI 가 아닌가** | 제작 환경이 Linux 샌드박스이고 NuGet 이 막혀 있어 Windows 용 .NET 10 런타임/데스크톱 팩이 없습니다. 즉 `dotnet publish -r win-x64 --self-contained` 는 **실행해 보지 못했고**, 검증하지 못한 WPF 코드를 드리는 것보다 빌드·테스트가 확인되는 현재 구조를 택했습니다(요청서의 "안정성 우선" 원칙). 단일 파일 EXE 는 달성했고(라이브러리 내장), x64 전용 self-contained 는 아닙니다(AnyCPU). |
| **Windows 에서의 실제 동작** | 이 환경에서는 Windows 를 실행할 수 없어서 **Chrome/Selenium 실제 등록, DWM 제목표시줄, 125~200% DPI, 사이드바 아이콘 글꼴은 직접 확인하지 못했습니다.** `3_소스코드/tests/WINDOWS_CHECKLIST.md` 의 19개 항목을 한 번씩 확인해 주세요. |

## 폴더
```
1_배포용_앱/          사용자에게 배포하는 파일 (BunjangAuto.exe, 상품양식.csv)
2_관리자용_발급툴/     LicenseAdmin.exe, license_private.xml, issued_licenses.csv  ← 사용자에게 절대 배포 금지
3_소스코드/           전체 소스, build.sh / build.bat / build_admin.sh, BunjangAuto.csproj, tests/
4_중앙로그서버/        Python 서버 + 관리자 웹 대시보드 + DB 스키마 + Docker (README.md 참고)
5_기준선_원본소스/     v1 MainForm.cs (비교용)
6_미리보기_스크린샷/   Mono 로 그린 화면 (레이아웃 참고용, 실제 Windows 모습과 글꼴·모서리가 다릅니다)
BunjangAuto.sln
```
`license_private.xml` 은 사용자용 EXE 에 들어 있지 않음을 확인했습니다(EXE 바이너리에서 개인키 조각 검색 → 없음).

## 기존 기능은 어떻게 보존했나
원본 ZIP 과 파일 단위로 비교한 결과:
- **한 글자도 안 바뀐 파일**: `License.cs`(RSA 검증), `Vault.cs`(암호화), `Guard.cs`, `Storage.cs`(CSV·저장·마이그레이션), `Schedule.cs`, `Selectors.cs`, `CategoryStore.cs`, `admin/LicenseAdmin.cs`, `kg.cs`, `license_public.xml`
- **추가만 한 파일**: `Bot.cs`(+15줄: 일시정지 지점, Selenium 시작/종료 로그), `Runner.cs`(+35줄: 일시정지, 현재 상품, 구조화 로그), `LicenseForm.cs`(+26줄: 라이선스 정보 보관, 키 교체 함수), `Models.cs`(`ThemeMode` 필드 1개, 기본값 System → 예전 settings.json 그대로 열림)
- **Log**: `Log.Write(string)` / `Log.Line` 과 `%AppData%\BunjangAuto\logs\yyyyMMdd.log` 형식은 그대로. 같은 호출이 자동으로 구조화(Level/Module/Action/Result)되어 `.jsonl` 에도 기록되고 중앙 큐로 들어갑니다. 예전 로그 문구는 한 글자도 바뀌지 않았습니다.
- 예전 **MainForm 의 실행/예약 로직**(1개씩, 시간대별, 반복, 중복 허용, 다음 실행, 자동 종료)은 `AutomationController` 로 **그대로 옮겼습니다**(UI 와 분리).
- **데이터 호환**: 구버전 EXE 가 만든 암호화 `settings.json`/`products.json` 을 신버전이 열어 12개 상품 필드·설정이 전부 보존됨을 실제로 테스트했습니다(`tests/regression.sh`).

### 바뀐/달라진 점 (의도적)
1. 화면: 탭 → 사이드바 구조. 예전 "실행/예약" 탭이 **자동 등록**(실행·진행)과 **예약**(간격·시간대·반복 설정)으로 나뉨. 예약 설정은 값을 바꾸는 즉시 저장(예전엔 시작/종료 때 저장).
2. **이용약관 제7조 개정**(운영 로그·시스템 정보 전송 고지) + 약관 버전 상승 → **모든 사용자가 다시 동의**해야 합니다. 중앙 로그는 약관 동의·라이선스 확인 **이후에만** 시작됩니다.
3. 메시지 상자를 테마가 적용되는 자체 상자로 교체(문구 동일).
4. 새 로그 줄이 몇 개 추가됨(예: `Selenium 세션을 시작했습니다`, `상품을 추가했습니다`, `설정을 바꿨습니다`). 기존 줄은 그대로.
5. 기본 글꼴/색: 금색 테마 → Windows 11 Fluent 색(액센트 파랑).

## 새로 추가된 것
- **Sidebar UI**: Dashboard · 상품 관리 · 자동 등록 · 로그인 · 예약 · 로그 · 사용 설명서 · 설정 · 라이선스 · (관리자 로그인 시) 관리자 센터. 헤더에 현재 페이지, 라이선스 상태·D-day, USER/ADMIN, 테마 선택.
- **Dashboard**: 등록 대기/성공/실패/전체, 로그인 상태, 라이선스 상태·만료일·남은 기간, 최근 활동·최근 로그(실제 데이터, 첫 실행에도 안내 문구).
- **상품 관리**: 기존 모든 열·편집·CSV 유지 + 검색/정렬/필터(화면 전용, 등록 순서는 안 바뀜).
- **자동 등록**: 시작 / **일시정지(신규)** / 중지, 현재 상품, 진행률, 성공·실패·남은 상품, 다음 실행 시각.
- **로그 화면**: 시간·Level·Module·Action·Message·Result, 검색/Level 필터, 자동 스크롤, 이전 기록 불러오기.
- **사용 설명서(EXE 내장, 오프라인)**: 20개 문서(요청 목차 전부 + 설정/FAQ), 왼쪽 목차 + 오른쪽 본문 + 검색·강조.
- **Light / Dark / System**: 헤더와 설정에서 즉시 변경, `settings.json` 에 저장되어 재실행 후 유지, Windows 앱 모드 변경 실시간 추적, 모든 화면·대화상자가 같은 팔레트 사용, 다크는 5단계 계층 색.
- **중앙 로그/텔레메트리**(`TelemetryService`, `CentralLogService`, `CrashReportService`, `UsageMetricsService`, `ClientIdentityService`, `Redactor`, `LogQueue`): 비동기·배치·디스크 보관·지수 백오프, 네트워크 실패가 자동등록에 영향 없음, 비밀정보 제거 후 전송, 크래시 보고.
- **관리자**: 웹 대시보드 + EXE 안 관리자 센터(Users / Live Logs / Errors / Statistics / 사용자 상세 / CSV·JSON 내보내기), 서버 측 ADMIN 인증.
- **SelfTest 확장**: Theme, Storage Migration, Telemetry Serialization, Redaction, Log Queue, Client ID, CSV Compatibility, 전송 계층 — 전체 98개 통과.
- EXE 정보: 제품명 BunjangAuto, 제목 "번개장터 자동등록", 회사 음악천재, 버전 2.0.0.0, Copyright, 고해상도 아이콘(16~256px). 고DPI 는 시작 시 PerMonitorV2 로 설정(Windows 빌드 `build.bat` 는 매니페스트도 내장).

## 중앙 로그 서버 실행 · 관리자 대시보드 접속
```bash
cd 4_중앙로그서버
python3 server.py init-admin --username admin     # 관리자 비밀번호 설정(12자 이상)
python3 server.py serve --port 8080               # → 브라우저 http://서버주소:8080/ 로그인
cat data/ingest_keys.txt                          # EXE 에 넣을 등록 키
```
운영은 반드시 HTTPS(리버스 프록시 또는 `--cert/--key`)로 하세요. 자세한 내용은 `4_중앙로그서버/README.md`.
EXE 연결: `BJA_SERVER_URL=https://logs.example.com BJA_INGEST_KEY=<키> ./build.sh` 로 빌드하거나, 사용자 PC 의 `%AppData%\BunjangAuto\telemetry.json` 수정.
EXE 안 관리자 센터: **설정 → 관리자 로그인** (서버가 ADMIN 으로 확인해 줄 때만 사이드바에 표시).

## 빌드
| 환경 | 명령 | 상태 |
|---|---|---|
| Linux/macOS (Mono) | `cd 3_소스코드 && ./build.sh` (+ `./build_admin.sh`) | **검증됨** |
| Windows (VS/SDK 불필요) | `build.bat` — 기본 포함된 .NET Framework `csc.exe`, 아이콘·매니페스트 내장 | 미실행 (C# 5 문법만 사용하는 것은 확인) |
| Windows (`dotnet`/VS) | `BunjangAuto.sln` / `BunjangAuto.csproj` (net472) | **미검증** |

## 배포 방법
1. (선택) 서버 주소/키를 넣어 빌드 → `BunjangAuto.exe`
2. `1_배포용_앱/` 의 exe 와 `상품양식.csv` 만 사용자에게 전달 (설치 불필요)
3. 사용자는 실행 → 약관 동의 → PC 고유번호를 관리자에게 전달 → `LicenseAdmin.exe`(관리자 PC)로 키 발급 → 인증
4. `2_관리자용_발급툴/`, `license_private.xml`, 서버 `data/` 는 절대 배포하지 마세요.

## 검증 결과 (요청서 44개 확인 항목 기준)
| 구분 | 항목 | 상태 |
|---|---|---|
| 자동 검증됨 | 5 PC ID·라이선스 검증 로직(코드 무변경 + 기존 SelfTest), 7·8·9 Storage·settings.json·products.json(구버전 데이터 마이그레이션 테스트), 10~12 CSV 불러오기/저장/양식(왕복·예전 형식), 33 SelfTest(98개), 34 LicenseAdmin 빌드, 35~37 테마 3종(팔레트·글자 대비 검사 + Mono 화면 렌더링), 38 도움말(렌더링), 39~44 중앙 로그·관리자 Dashboard·사용자별 로그·오류·통계·Export(서버 테스트 8개 + EXE→서버 종단 + 대시보드 jsdom) | ✅ |
| 화면 표시만 확인(Mono) | 1 앱 실행(MainForm 이 만들어지고 9개 화면이 그려짐), 13~16 상품 추가/수정/삭제/이미지(기존 코드 이식, 편집 대화상자 렌더링 확인), 27~31 시작/중지/일시정지/진행률/로그(화면·상태 계산 확인) | ⚠️ Windows 에서 클릭 동작 확인 필요 |
| **Windows 에서만 확인 가능** | 2 단일 실행, 3 약관 화면, 4 라이선스 입력 화면, 6 Guard, 17~22 Chrome 실행·Profile·ChromeDriver·Selenium·로그인·실제 상품 등록, 23~26 반복/1개씩/시간대별/예약의 **실제 등록 동작**(스케줄 계산 자체는 `Schedule.cs` 무변경 + SelfTest 통과), 32 screenshots | ❌ 이 환경에서 불가능 → `WINDOWS_CHECKLIST.md` |

`3_소스코드/tests/regression.sh [원본ZIP]` 한 번으로 자동 검증 부분(빌드·SelfTest·서버·종단·대시보드·마이그레이션)을 다시 돌릴 수 있습니다 — 현재 **10/10 통과**.

## 알려진 한계 / 주의
- **WinUI/WPF/.NET 10, Mica 배경, 직접 그린 최소·최대·닫기 버튼은 구현하지 않았습니다.** Windows 의 기본 제목 표시줄을 쓰되 Windows 11 에서는 다크 모드·제목 표시줄 색·둥근 모서리를 DWM 으로 맞춥니다.
- DPI(100~200%)는 코드로 대응했지만 실제 Windows 에서 테스트하지 못했습니다. 문제가 있으면 알려 주세요.
- 사용자가 `telemetry.json` 을 고쳐 전송을 끌 수 있습니다(차단하려면 별도 설계 필요). 설정 화면에 현재 전송 상태가 표시됩니다.
- 시스템 정보 수집은 개인정보에 해당할 수 있어 **약관 고지를 넣었지만 법률 검토는 받지 않았습니다.** 배포 전 확인을 권합니다.
- 등록 키(ingest key)는 EXE 에 들어가는 공개 값입니다. 관리자 자격과 무관하며, 유출되면 `data/ingest_keys.txt` 를 바꾸고 새 빌드를 배포하세요(기존 설치는 토큰으로 계속 전송).
- 서버는 SQLite 단일 서버(소규모용)입니다.
