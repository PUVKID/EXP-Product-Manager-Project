# Lumivolt v2.1.0 — 설치 전 실행형 ZIP

요청에 따라 **설치하지 않고 압축을 풀어 실행하는 EXE**를 먼저 제공합니다. C# WinForms / .NET Framework 구조, Selenium 상품 등록 로직, 기존 테마와 설정·상품 데이터 형식을 유지했습니다. 설치 프로그램은 이번 전달본에 없으며, 나중에 사용할 Inno Setup 구성은 소스에 포함했습니다.

## 지금 실행하기

1. ZIP 전체를 압축 해제합니다.
2. `1_배포용_앱/Lumivolt.exe`를 실행합니다. Windows 10/11, .NET Framework 4.7.2 이상, 사용 가능한 TPM/P-256 키, Chrome이 필요합니다.
3. 이용약관 확인 후 라이선스 창에서 **라이선스 파일 열기**를 누릅니다.
4. 이 작업을 수행한 PC와 같은 Windows 계정이라면 `2_관리자용_발급툴/이PC_시험용.lvlicense`를 선택하고 **인증**을 누를 수 있습니다. 이 파일은 실제 새 서명을 사용하며 **2026-11-13 오전 9시(한국 시간)**에 만료됩니다. 다른 PC/계정용 인증 우회 기능이 아닙니다.
5. 다른 PC에서는 **등록 요청 저장**으로 `.lvrequest`를 만들고 아래 발급 절차를 진행하세요.

기존 v2.0 RSA 라이선스는 호환되지 않아 재발급이 필요합니다. `%AppData%/BunjangAuto`의 상품, 설정, 이미지 경로, 예약, 로그인 프로필은 기존 위치와 형식을 사용합니다. 기존 PC 고유번호 기반 데이터 암호화는 그대로 유지하고 라이선스만 TPM 키에 귀속했습니다. 기존 파일을 삭제하지 않습니다.

## ZIP 구성

- `1_배포용_앱`: 고객에게 전달할 실행 EXE, 상품 CSV 양식, 라이선스 고지
- `2_관리자용_발급툴`: 별도 관리자 EXE, 공개키, 이 PC 시험용 라이선스, 사용 안내
- `3_소스코드`: 전체 수정 소스, 기존 라이브러리·리소스, 빌드·서명·설치 구성, 테스트
- `4_중앙로그서버`: 원본 서버 코드 보존. 새 라이선스 발급/검증은 이 서버를 사용하지 않음
- `5_검증결과`: 테스트 출력, 변경 목록과 SHA-256 비교, 변경 전후 Windows 렌더링

고객에게는 **1_배포용_앱 폴더만** 전달하세요. 전체 프로젝트 ZIP은 개발·관리자 보관용입니다.

## 관리자 발급

1. `2_관리자용_발급툴/LicenseAdmin.exe`를 실행합니다.
2. 이 PC에서는 생성된 발급키 보관 위치가 자동으로 연결됩니다. 필요하면 **발급키 관리 → 기존 발급키 보관 폴더 열기**로 선택하세요.
3. **요청 열기**로 고객의 `.lvrequest` 파일을 읽고 이름/메모, 만료일 또는 무기한을 설정합니다. 제품은 Lumivolt, 권한은 현재 제품의 단일 `automation` 권한입니다. 알 수 없는 제품·권한은 거부합니다.
4. **라이선스 키 발급 → 파일 저장** 후 `.lvlicense`를 고객에게 전달합니다. 복사한 키 문자열도 사용할 수 있습니다.
5. **발급 기록**에서 기존 파일을 조회하고 JSON으로 기록을 내보낼 수 있습니다. 만료된 발급 기록도 서명을 확인해 조회합니다.

이 전달본의 실제 발급키 보관 폴더:
`C:\Users\PUVKID\Documents\Lumivolt_AdminVault_20261013`

개인키는 이 폴더의 `issuer.dpapi`에 암호화되어 있으며 **어느 ZIP에도 포함하지 않았습니다**. 현재 Windows 계정의 DPAPI로 보호하고 폴더 접근 권한 상속을 끈 뒤 현재 계정에만 권한을 부여했습니다. 이 폴더를 삭제하지 마세요. 새 발급키를 만들면 현재 EXE와 일치하지 않습니다.

**발급키 관리 → 암호화 백업 만들기**에서 본인이 정한 12자 이상의 강한 암호로 백업을 만들고, 백업과 암호를 다른 안전한 장소에 보관하세요. 다른 관리자 PC에서는 **암호화 백업에서 복원**으로 같은 키를 복원할 수 있습니다. `issuer.dpapi`만 다른 Windows 계정에 복사하는 방식은 작동하지 않습니다. 공개키 ID를 비교해 복원이 같은 키인지 확인하세요. 발급 기록 `records`도 별도 백업합니다.

## Windows 빌드

Windows PowerShell에서:

```powershell
cd 3_소스코드
powershell -NoProfile -ExecutionPolicy Bypass -File build/Build.ps1
```

이 구성은 Windows의 .NET Framework C# 컴파일러를 사용하므로 별도 Visual Studio 설치 없이 이 환경에서 실제 빌드했습니다. 결과는 `dist/Lumivolt.exe`, `dist/LicenseAdmin.exe`입니다. 검증용과 관리자용 코드를 사용자 프로그램의 인증 우회 경로로 연결하지 않습니다. 내장 자가 진단 기능은 기존 설정 화면에서 계속 제공합니다.

새 관리자 키를 쓰는 경우 먼저 관리자 EXE에서 키를 생성하고 공개키 JSON을 내보낸 뒤:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/Build.ps1 -PublicKeyFile C:/safe/issuer-public.json
```

이 빌드는 공개키를 EXE 안에 고정합니다. 공개키 JSON을 고객 EXE 옆에 바꿔 넣는 방식으로 신뢰키를 변경할 수 없습니다. 새 키로 재빌드하면 과거 키로 발급한 라이선스는 승인하지 않으므로, 키 교체 때 고객 재발급·업데이트를 함께 계획하세요. 현재 빌드에는 제공된 공개키 한 개를 신뢰하도록 구성했습니다. SDK 프로젝트 `Lumivolt.csproj`도 갱신했지만 `dotnet build` 경로는 SDK가 없어 별도로 실행하지 않았습니다. Windows TPM API 때문에 예전 Mono/Linux 빌드 스크립트는 안내 후 종료합니다.

## 테스트

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/Test.ps1
```

테스트 EXE는 `dist`에만 생성합니다. 회귀/UI 테스트는 `TESTBUILD` 전용 임시 데이터 경로를 사용합니다. 테스트용 개인키는 실행 때 메모리/임시 보관 폴더에서 생성 후 폐기하며 배포용 신뢰키에 추가하지 않습니다. TPM 테스트는 현재 계정의 `Lumivolt.Device.v3` 키를 생성하거나 다시 사용합니다.

실행 결과: 보안 47개, 기존 회귀 110개, 인증 통합 11개, 실제 고객 EXE 확인 3개 통과. 라이트/다크 20개 화면과 관리자 화면을 렌더링했습니다. 상세 범위와 미검증 항목은 `5_검증결과/TEST_RESULTS.md`를 확인하세요.

## 설치 전환 및 코드 서명

현재 산출물은 실행형 EXE이며 설치 관리자 EXE는 생성하지 않았습니다. 이후 Inno Setup 6 환경에서:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/Build-Installer.ps1 -IsccPath 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

`installer/Lumivolt.iss`는 사용자별 설치, 설치 경로 선택, 시작 메뉴/선택적 바탕 화면 바로가기, 앱 제거 등록, 동일 AppId 업그레이드, .NET 사전 검사, 설치 로그, 명시적 배포 파일 목록을 설정합니다. 사용자 AppData와 TPM 키를 제거하는 규칙은 없습니다. 실제 설치·업그레이드·제거 테스트는 이번 단계에서 수행하지 않았습니다.

Windows 코드 서명 인증서가 제공되지 않아 두 EXE는 Authenticode **미서명**입니다. 라이선스의 Ed25519 서명과는 별개입니다. 인증서가 준비되면 `build/Sign.ps1`에 SignTool 경로, 인증서 지문, 타임스탬프 URL을 전달해 서명하고 확인할 수 있습니다.
