# 보안 구조와 운영 한계

## 키 역할

- 라이선스 발급: Bouncy Castle.Cryptography 2.7.0의 Ed25519. 고객 EXE에는 `src/Security/TrustedKeys.cs`의 공개키만 고정됩니다. 이전 RSA 개인키·발급 헬퍼는 배포 고객 코드에서 제거했습니다.
- 기기 귀속: Windows CNG `Microsoft Platform Crypto Provider`, P-256 ECDSA, 영구 키 이름 `Lumivolt.Device.v3`, 현재 사용자 범위, Signing 용도, `CngExportPolicies.None`. 다른 공급자로 자동 대체하지 않습니다.
- 기기 키 생성은 등록 요청 내보내기 때 수행합니다. 라이선스 검증 때 키가 없으면 새로 만들지 않고 거부합니다. 매 검증에서 내장 라이선스 공개키 서명 검증 후 로컬 TPM 키의 공개키 일치와 새 32바이트 난수의 서명/검증을 수행합니다. 문자열 PC ID만으로 인증하지 않습니다.
- AppData의 설정·상품 데이터 복호화용 기존 MachineGuid 기반 번호는 호환성용입니다. 안전한 라이선스 자격 증명으로 사용하지 않으며 기존 데이터 암호화의 보안 수준까지 높였다고 주장하지 않습니다.

## 파일 형식

라이선스: `LV3.Base64(원본 UTF-8 JSON).Base64(Ed25519 서명)`.
요청: `LVR3.Base64(원본 UTF-8 JSON).Base64(P-256 서명)`.

JSON 발급은 BOM 없는 UTF-8, 압축 JSON, 명시적 필드 삽입 순서, 정수 버전 3, UTC `yyyy-MM-ddTHH:mm:ssZ`입니다. 검증은 **받은 payload 원본 바이트**를 검증하며 재직렬화된 바이트를 서명 대상으로 사용하지 않습니다. 서명된 다른 공백/순서도 원본 바이트 기준으로 검증됩니다. 중복/누락/알 수 없는 필드, 잘못된 타입·키 크기·UTF-8·시간·제품·버전·권한·상태를 거부합니다. 파일 32KiB 및 payload 16KiB 제한, JSON 깊이 제한을 적용합니다.

필드: version, licenseId(UUID), product, name, issuedUtc, expiresUtc(null 또는 배타적 만료 UTC), perpetual, features, devicePublicKey(CNG ECC public blob), keyId(Ed25519 공개키 SHA-256의 앞 128비트), status(active).

요청 서명은 `Lumivolt.Request.v3\n` 접두 바이트와 원본 payload에 적용합니다. 요청 ID, 생성 시각, 공개키, 공급자 표시, 32바이트 난수를 포함하며 유효기간 30일입니다. 개인키는 포함하지 않습니다.

요청 증명은 **해당 공개키의 개인키 소유 증명**입니다. 원격 TPM attestation은 구현하지 않았으므로 공급자 문자열/요청 서명만으로 고객 기기가 물리 TPM이라는 사실을 관리자가 독립 검증할 수는 없습니다. 정상 고객 앱은 Platform KSP만 사용하며 실행 때 그 키와 라이선스 공개키가 일치해야 합니다. 이 차이를 고급 변조 방지 또는 TPM 원격 증명으로 과장하지 않습니다.

## 실패 처리

TPM 접근 실패, 분실, 서명 오류, 손상 파일, 만료, 권한 불일치는 인증 거부로 처리합니다. UI의 LastInfo는 표시용이며 실제 실행 권한은 원본 License.Verify 및 LicenseGate.Recheck를 사용합니다. 앱 시작, 기존 2분 주기 Guard, 실행 시작, 상품마다 다시 확인합니다. 이미 진행 중인 단일 브라우저 단계가 만료 순간 즉시 중단되는 실시간 원격 통제는 아닙니다.

최근 인증 시각은 DPAPI로 보호한 `license.clock.v3`에 원자적으로 저장하며 손상·쓰기 실패·5분보다 큰 역행은 거부합니다. 사용자가 이 파일이나 Windows 스냅샷 전체를 복원/삭제하는 상황까지 신뢰할 수 있는 오프라인 시계는 아닙니다.

## 관리자 키

실제 키는 ZIP 외부의 전용 관리자 폴더에 DPAPI(CurrentUser)로 저장합니다. 키 생성 때 폴더는 비어 있어야 하고, ACL 상속을 차단해 현재 계정만 허용합니다. 키 재생성으로 기존 키를 덮어쓰지 않습니다. 일반 앱의 사용자 데이터 폴더, 소스, 로그, 고객 배포 폴더에는 개인키를 저장하지 않습니다.

관리자 도구의 이동형 백업은 .NET PBKDF2-HMAC-SHA256(600,000회, 32바이트 salt)과 Bouncy Castle AES-256-GCM(12바이트 nonce, 128비트 tag, 고정 용도 AAD)을 사용합니다. 암호는 12자 이상이어야 하며 강한 독립 암호를 권장합니다. 새 nonce/salt를 사용하고 잘못된 암호·변조는 거부합니다. 복원은 비어 있는 새 관리자 폴더에만 허용합니다. 백업도 고객에게 배포하지 않습니다. 복원 후 공개키 ID와 기존 발급 내역 서명을 확인하세요.

관리자 권한 또는 관리자 계정 자체를 탈취한 공격자로부터 일반 DPAPI/파일 ACL이 완전한 보호를 제공하지는 않습니다. 장기 운영 시 전용 관리자 PC, 디스크 암호화, 별도 오프라인 백업을 사용하세요.

## 한계와 재발급

완전한 로컬 실행 환경에서는 EXE 패치, 메모리 변조, 고급 VM/vTPM 복제, 시각 조작을 완전히 막을 수 없습니다. 상시 인증 서버가 없으므로 즉시 원격 폐기·동시 사용 탐지·중앙 중복 등록 차단을 제공하지 않습니다. 기록에서 폐기했다고 이미 오프라인 전달된 라이선스가 무효화되지 않습니다. 기간제 발급으로 노출 기간을 줄일 수 있습니다.

TPM 초기화/메인보드 교체/Windows 계정 교체 시 새 요청을 받아 고객을 확인하고 재발급합니다. 이전 라이선스 ID의 폐기 기록을 관리자 운영 기록으로 보관하세요. 기기 키는 내보내거나 다른 PC로 복사하지 않습니다. 이번 작업에서 TPM 초기화/재부팅/보안 설정 변경은 수행하지 않았습니다.

기존 선택형 중앙 로그 기능과 서버는 보존했습니다. 새 오프라인 라이선스 시스템은 네트워크 연결을 요구하지 않습니다. 실제 번개장터 등록 기능은 기존 Chrome/Selenium과 네트워크를 사용합니다.

## 참고한 1차 문서

- [Microsoft CNG key storage providers](https://learn.microsoft.com/en-us/windows/win32/seccertenroll/cng-key-storage-providers)
- [Microsoft CngKey export policy](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cngkeycreationparameters.exportpolicy)
- [Bouncy Castle C# 공식 배포·릴리스 정보](https://www.bouncycastle.org/download/bouncy-castle-c/)

라이선스 서명과 Windows Authenticode는 별개입니다. 이 배포 EXE에 코드 서명 인증서를 적용했다고 주장하지 않습니다.
