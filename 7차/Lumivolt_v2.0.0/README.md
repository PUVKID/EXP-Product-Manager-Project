# Lumivolt (루미볼트) 2.0.0  — 음악천재

> 제품명이 **BunjangAuto / 번개장터 자동등록 → Lumivolt(루미볼트)** 로 바뀌었습니다. (이전 버전의 데이터를 그대로 읽기 위해 데이터 폴더 이름 `%AppData%\BunjangAuto` 와 내부 코드 이름은 일부러 그대로 둡니다.)
> 이름은 코드 한 곳(`AssemblyInfo.cs`, `MainForm.cs` 등 화면 문구)과 빌드 스크립트의 출력 이름만 바꾸면 다시 바꿀 수 있습니다. 상표 선점 여부는 확인하지 못했으니 사용 전 상표 검색을 해 보세요.

기존 v1(BunjangAuto)의 기능을 기준선으로 두고, UI 를 Windows 11 스타일로 바꾸고 관리자용 중앙 로그/통계, 내장 사용 설명서, Light/Dark/System 테마를 추가한 버전입니다.

## 먼저 알려 드릴 것 (정직한 요약)

| 질문 | 답 |
|---|---|
| **빌드가 성공했나요?** | **예.** `3_소스코드/build.sh`(Mono `mcs`)로 Release 빌드 성공, `Lumivolt.exe`(약 14MB, 나눔고딕 글꼴 포함 단일 파일)와 `LicenseAdmin.exe` 생성. |
| **EXE 경로** | `1_배포용_앱/Lumivolt.exe` (원본 빌드 위치: `3_소스코드/dist/`) · 관리자 발급툴 `2_관리자용_발급툴/LicenseAdmin.exe` |
| **.NET 버전** | **.NET Framework 4.7.2** (Windows 10 1803+ / Windows 11 에 기본 포함 → 설치 없이 실행). **.NET 10 이 아닙니다.** |
| **UI 프레임워크** | **WinForms + 직접 그린 Fluent 컨트롤**(카드·사이드바·토글·세그먼트·차트 등). **WinUI 3/WPF 가 아닙니다.** |
| **왜 .NET 10/WPF/WinUI 가 아닌가** | 제작 환경이 Linux 샌드박스이고 NuGet 이 막혀 있어 Windows 용 .NET 10 런타임/데스크톱 팩이 없습니다. 즉 `dotnet publish -r win-x64 --self-contained` 는 **실행해 보지 못했고**, 검증하지 못한 WPF 코드를 드리는 것보다 빌드·테스트가 확인되는 현재 구조를 택했습니다(요청서의 "안정성 우선" 원칙). 단일 파일 EXE 는 달성했고(라이브러리 내장), x64 전용 self-contained 는 아닙니다(AnyCPU). |
| **Windows 에서의 실제 동작** | 이 환경에서는 Windows 를 실행할 수 없어서 **Chrome/Selenium 실제 등록, DWM 제목표시줄, 125~200% DPI, 사이드바 아이콘 글꼴은 직접 확인하지 못했습니다.** `3_소스코드/tests/WINDOWS_CHECKLIST.md` 의 19개 항목을 한 번씩 확인해 주세요. |

## 폴더
```
1_배포용_앱/          사용자에게 배포하는 파일 (Lumivolt.exe, 상품양식.csv)
2_관리자용_발급툴/     LicenseAdmin.exe, license_private.xml, issued_licenses.csv  ← 사용자에게 절대 배포 금지
3_소스코드/           전체 소스, build.sh / build.bat / build_admin.sh, Lumivolt.csproj, tests/
4_중앙로그서버/        Python 서버 + 관리자 웹 대시보드 + DB 스키마 + Docker (README.md 참고)
5_기준선_원본소스/     v1 MainForm.cs (비교용)
6_미리보기_스크린샷/   Mono 로 그린 화면 (레이아웃 참고용, 실제 Windows 모습과 글꼴·모서리가 다릅니다)
Lumivolt.sln
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
- **SelfTest 확장**: Theme, Storage Migration, Telemetry Serialization, Redaction, Log Queue, Client ID, CSV Compatibility, 전송 계층 — 전체 110개 통과.
- EXE 정보: 제품명 Lumivolt, 제목 "루미볼트 Lumivolt", 회사 음악천재, 버전 2.0.0.0, Copyright, 고해상도 아이콘(16~256px). 고DPI 는 시작 시 시스템 DPI 인식으로 켜고 화면 배율은 코드가 직접 적용합니다(Windows 빌드 `build.bat` 는 매니페스트도 내장).
- **글꼴: 나눔고딕 Bold**(NAVER, SIL OFL 1.1) — TTF 가 exe 안에 들어 있어 설치가 필요 없고, 시작할 때 이 프로그램 안에서만 쓰도록 등록합니다. 글꼴이 Bold 한 종류라서 모든 글자가 굵게 나옵니다(제목/본문은 크기와 색으로 구분). 등록에 실패하면 Windows 기본 글꼴로 자동 대체되며, 설정 화면과 SelfTest 에 "글꼴: 적용/대체"가 표시됩니다. 라이선스 전문은 `3_소스코드/resources/fonts/NanumGothic-OFL.txt`.

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
| Windows (`dotnet`/VS) | `Lumivolt.sln` / `Lumivolt.csproj` (net472) | **미검증** |

## 배포 방법
1. (선택) 서버 주소/키를 넣어 빌드 → `Lumivolt.exe`
2. `1_배포용_앱/` 의 exe 와 `상품양식.csv` 만 사용자에게 전달 (설치 불필요)
3. 사용자는 실행 → 약관 동의 → PC 고유번호를 관리자에게 전달 → `LicenseAdmin.exe`(관리자 PC)로 키 발급 → 인증
4. `2_관리자용_발급툴/`, `license_private.xml`, 서버 `data/` 는 절대 배포하지 마세요.

## 검증 결과 (2차 전체 재검증 포함)
`3_소스코드/tests/regression.sh [원본ZIP]` 한 번으로 아래 **23개 묶음이 모두 통과**합니다.

| 검증 | 내용 | 결과 |
|---|---|---|
| 빌드 | Lumivolt.exe, LicenseAdmin.exe | ✅ |
| SelfTest | 기존 항목 + v2 추가(테마·**글꼴**·**도움말 20개 목차**·**일시정지**·저장 호환·직렬화·비밀정보 제거·로그 큐·Client ID·CSV·전송 계층) — 110개 | ✅ |
| 문법 호환 | `mcs -langversion:5` 로 전체 컴파일 → Windows 기본 `csc`(C# 5)도 받아들이는 문법만 사용 | ✅ |
| UI 상호작용 | 가상 화면에서 실제로 컨트롤을 조작: 9개 화면 이동, 테마 3종 전환·저장·전 화면 색 일치, 상품 검색/필터/정렬, 로그 실시간·필터, 도움말 검색, **자동 등록 시작→다음 실행 대기→중지 흐름**, 예약 설정 즉시 저장, **라이선스 발급·검증(정상/다른 PC/만료/변조/무기한)** — 38개 | ✅ |
| 배율 레이아웃 | 100·125·150·175·200% × 라이트/다크 = 10조합에서 모든 화면의 **잘림·겹침·부모 밖 이탈 없음**(글꼴도 같이 키운 에뮬레이션) | ✅ |
| 서버 | 단위 8개 + 보강 19개(SQL 주입, 경로 탐색, 1MB 초과, 이벤트 501건, 토큰 위조, 필드 길이, **동시 60접속×100건**, 500건 1초 이내, 로그아웃 무효화, **TLS 직접 제공·1.1 이하 거부**) | ✅ |
| 종단 | EXE 전송 → 서버 저장 → 비밀번호 값 제거 확인 → 관리자 CSV → 인증 없이 401 → 관리자 대시보드 화면 동작(jsdom) | ✅ |
| 마이그레이션 | v1 EXE 가 만든 암호화 settings/products → v2 가 열어 12개 필드 전부 보존 | ✅ |
| 오프라인 | 서버 포트 닫힘/없는 도메인에서도 0.9초 안에 실패 처리, 예외 없음, 기록은 유지 | ✅ |
| 크래시 | 처리되지 않은 예외 → `crash-*.txt` + FATAL 로그, UI 예외는 ERROR | ✅ |

### 이번 재검증에서 **새로 찾아 고친 문제**
1. **(서버) 동시 접속 시 연결 거부**: 수신 대기열이 기본 5라 여러 사용자가 동시에 켜면 일부 연결이 끊겼음 → 256으로 확대, 60접속 12회 반복 통과.
2. **(서버) 거절한 POST 의 본문이 다음 요청으로 오해될 수 있음** → 오류 응답 시 연결을 닫도록 수정.
3. **(화면) Dashboard "전체 보기" 버튼이 카드 밖에 있어 보이지 않던 문제**(100%에서도).
4. **(화면) 예약 화면 카드 높이가 모자라 맨 아래 "반복 횟수"가 잘리던 문제**(100%에서도).
5. **(화면) 도움말 왼쪽 검색칸/목록이 카드 밖으로 넘치던 문제**.
6. **(DPI 구조) 나중에 만든 화면에 WinForms 자동 배율이 적용되지 않는 구조 결함** → 배율을 직접 적용(`Ui.ScaleTree/ScaleForm`)하도록 전면 수정하고 위 배율 검사로 확인.
7. **(비밀정보 제거) 화면 캡처 파일명 `20261009-142005` 같은 날짜·시각 숫자가 카드번호로 오인되어 가려지던 문제** → 카드사 번호(IIN) 규칙 추가.
8. **(도움말) RichTextBox 서식이 환경에 따라 달라지는 문제** → 직접 그리는 문서 뷰어(`DocView`)로 교체(줄바꿈·스크롤·검색 강조·전체 복사).
9. 서버 연결 실패 사유가 "등록 실패"로만 보이던 진단 문구 개선.

### 요청서 44개 확인 항목별 상태
| 구분 | 항목 | 상태 |
|---|---|---|
| 자동 검증됨 | 5 PC ID·라이선스 검증, 7·8·9 Storage·settings·products, 10~12 CSV, 13~15 상품 필터/정렬/표시, 27~31 시작·중지·일시정지·진행률·로그(컨트롤러 흐름), 33 SelfTest, 34 LicenseAdmin, 35~37 테마, 38 도움말, 39~44 중앙 로그·관리자·통계·오류·Export | ✅ |
| 화면 표시만 확인 | 1 앱 실행(Mono 위에서 MainForm 이 뜨고 모든 화면 조작), 16 이미지 지정(코드 이식 + 편집 창 렌더링) | ⚠️ Windows 에서 확인 필요 |
| **Windows 에서만 확인 가능(미확인)** | 2 단일 실행, 3 약관 화면, 4 라이선스 입력 화면, 6 Guard, 17~22 Chrome·Profile·ChromeDriver·Selenium·로그인·**실제 상품 등록**, 23~26 반복/1개씩/시간대별/예약의 **실제 등록 동작**(스케줄 계산은 `Schedule.cs` 무변경 + SelfTest 통과), 32 screenshots, 일시정지가 **실제 Selenium 작업 중** 멈추는지 | ❌ 이 환경에서 불가능 → `tests/WINDOWS_CHECKLIST.md` |

## 알려진 한계 / 주의
- **WinUI/WPF/.NET 10, Mica 배경, 직접 그린 최소·최대·닫기 버튼은 구현하지 않았습니다.** Windows 의 기본 제목 표시줄을 쓰되 Windows 11 에서는 다크 모드·제목 표시줄 색·둥근 모서리를 DWM 으로 맞춥니다.
- **DPI**: 100~200% 는 "배율과 글꼴을 같이 키운 에뮬레이션"으로 레이아웃을 검사했을 뿐, 실제 Windows 의 DPI 설정에서 본 것은 아닙니다. 여러 모니터의 배율이 서로 다를 때는 시스템 DPI 인식 방식이라 창을 옮기면 약간 흐려질 수 있습니다.
- **글꼴**: 나눔고딕 등록은 Windows 의 `AddFontResourceEx(FR_PRIVATE)` 를 쓰는 표준적인 방법이지만 이 환경에서는 Windows 로 확인하지 못했습니다(Linux 에서는 시스템에 설치된 글꼴로 확인). 실패하면 자동으로 기본 글꼴이 쓰이고 설정 화면에 표시됩니다. 그럴 때는 `3_소스코드/resources/fonts/NanumGothicBold.ttf` 를 한 번 설치하면 됩니다.
- 스크롤바와 콤보박스 목록 등 일부 기본 컨트롤은 Windows 기본 모양이라 다크 모드에서도 밝게 보일 수 있습니다.
- 사용자가 `telemetry.json` 을 고쳐 전송을 끌 수 있습니다(차단하려면 별도 설계 필요). 설정 화면에 현재 전송 상태가 표시됩니다.
- 시스템 정보 수집은 개인정보에 해당할 수 있어 **약관 고지를 넣었지만 법률 검토는 받지 않았습니다.** 배포 전 확인을 권합니다.
- 등록 키(ingest key)는 EXE 에 들어가는 공개 값입니다. 관리자 자격과 무관하며, 유출되면 `data/ingest_keys.txt` 를 바꾸고 새 빌드를 배포하세요(기존 설치는 토큰으로 계속 전송).
- 서버는 SQLite 단일 서버(소규모용)입니다.
