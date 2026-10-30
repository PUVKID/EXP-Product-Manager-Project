const fs=require('fs'),p=require('path');
const base=p.resolve('work/round11'),src=p.join(base,'source'),stage=p.join(base,'delivery/EXP_v2.2.1');
fs.mkdirSync(stage,{recursive:true});
function write(file,text){let out=p.join(stage,file);fs.mkdirSync(p.dirname(out),{recursive:true});fs.writeFileSync(out,'\ufeff'+text);}
function copy(a,b){let out=p.join(stage,b);fs.mkdirSync(p.dirname(out),{recursive:true});fs.copyFileSync(a,out);}
function tree(from,to){fs.mkdirSync(to,{recursive:true});for(const entry of fs.readdirSync(from,{withFileTypes:true})){if(['dist','bin','obj','.git'].includes(entry.name))continue;const f=p.join(from,entry.name),t=p.join(to,entry.name);if(entry.isDirectory())tree(f,t);else fs.copyFileSync(f,t);}}
tree(src,p.join(stage,'3_소스코드'));
copy(p.join(src,'dist/installer/EXP_Setup_2.2.1.exe'),'1_고객배포/EXP_Setup_2.2.1.exe');
copy(p.join(src,'dist/EXP.exe'),'2_윤석님용/설치없이확인/EXP.exe');
copy(p.join(src,'dist/EXP-Master.exe'),'2_윤석님용/EXP-Master.exe');
for(const f of fs.readdirSync(p.join(src,'licenses')))copy(p.join(src,'licenses',f),'1_고객배포/licenses/'+f);
copy(p.join(src,'resources/fonts/NanumGothic-OFL.txt'),'1_고객배포/licenses/NanumGothic-OFL.txt');
for(const f of fs.readdirSync(p.join(stage,'1_고객배포/licenses')))copy(p.join(stage,'1_고객배포/licenses',f),'2_윤석님용/licenses/'+f);
for(const f of ['security-results.txt','regression-results.txt','release-results.txt','gate-results.txt','production-results.txt','ui-results.txt','admin-ui-results.txt','build-results.txt'])if(fs.existsSync(p.join(base,f)))copy(p.join(base,f),'4_검증결과/'+f);
for(const f of fs.readdirSync(p.join(base,'ui')))copy(p.join(base,'ui',f),'4_검증결과/화면/'+f);
for(const f of fs.readdirSync(base).filter(x=>/^master-(Light|Dark)-\d\.png$/.test(x)))copy(p.join(base,f),'4_검증결과/화면/'+f);
copy(p.resolve('work/round9/Lumivolt_v2.2.0/Lumivolt.sln'),'EXP.sln');
let sol=p.join(stage,'EXP.sln');fs.writeFileSync(sol,fs.readFileSync(sol,'utf8').replace(/Lumivolt/g,'EXP'));
fs.renameSync(p.join(stage,'3_소스코드/Lumivolt.csproj'),p.join(stage,'3_소스코드/EXP.csproj'));
write('먼저읽기.txt',`EXP 2.2.1 · 11차

고객에게 줄 파일: 1_고객배포/EXP_Setup_2.2.1.exe
윤석님이 실행할 파일: 2_윤석님용/EXP-Master.exe
설치 없이 먼저 확인: 2_윤석님용/설치없이확인/EXP.exe (같은 인증 필요)

전체 패키지에는 관리자 도구와 소스가 있습니다. 고객에게는 고객용 설치 파일만 전달하세요.
개인 발급키는 이 압축파일에 포함하지 않았습니다. 현재 PC의 기존 보안키를 EXP Master가 자동으로 연결합니다.

Classic: 하루 25개. 기본 등록과 기본 간격 등록.
Studio: 하루 50개. 반복·중복 등록과 시간대 예약 포함.
Pro: 하루 100개. Studio 기능과 추가 등록권 충전 포함.
Pro 체험판: 발급 시점부터 3시간. 같은 PC 또는 신청 이메일에는 재발급 차단.

기간제·무기한 발급 유지. 고객이 등급을 고르는 대신 서명된 키의 등급으로 설치됩니다.
기본 한도는 한국시간 자정 초기화, 미사용 기본 한도는 이월되지 않습니다.
구매한 추가 등록권 잔액만 이월합니다. 계정 변경 시에도 같은 PC 사용량을 합산합니다.
앱에서 확인한 계정별 등록도 하루 50개로 제한합니다. 앱 밖에서 사용한 등록량까지 조회하는 기능은 아닙니다.

상품은 앱에서 직접 작성·편집·사진 추가합니다. 고객 화면에 외부 상품 파일 가져오기/내보내기가 없습니다.
양쪽 앱에 나눔고딕 Bold를 내장했습니다. 관리자 라이트/다크 선택은 저장됩니다.

배포 전 남은 확인: 실제 번개장터 계정의 내 상점 링크 인식과 상품 등록.
이번 검증에서는 실제 상품을 게시하지 않았습니다. 인식할 수 없으면 안전하게 등록을 중단합니다.
Windows 코드서명 인증서는 제공되지 않아 EXE/설치 파일에 Authenticode 서명은 없습니다.
`);
write('1_고객배포/시작하기.txt',`EXP 시작하기

1. EXP_Setup_2.2.1.exe를 실행하세요.
2. 인증 화면에서 [등록코드 복사]를 눌러 판매자에게 보내 주세요.
3. 받은 인증키를 붙여 넣으면 해당 제품이 설치됩니다.
4. 앱에서 상품을 추가하고 사진·가격·설명을 입력한 뒤 등록을 시작하세요.

Pro 3시간 체험: 첫 인증 화면의 [체험판 요청 복사]를 누르고
expmasterxx00@gmail.com로 보내 주세요. 받은 체험 키를 같은 화면에 입력합니다.
체험 시간은 발급 시점부터 계산됩니다. 같은 PC와 신청 이메일에는 1회만 제공됩니다.

기본 일일 한도는 한국시간 자정에 초기화되고 누적되지 않습니다.
추가 등록권 구매 문의: expmasterxx00@gmail.com
`);
write('2_윤석님용/발급방법.txt',`윤석님용 EXP Master

정식 키
1. EXP-Master.exe를 엽니다. [발급 준비 완료]가 보이면 키 설정은 끝입니다.
2. 고객 등록코드를 붙여 넣습니다. 고객 메모는 비워도 됩니다.
3. Classic / Studio / Pro와 일수·만료일 또는 무기한을 고릅니다.
4. [발급하고 복사]를 눌러 고객에게 보내 주세요. 발급 기록은 자동 저장됩니다.
일수는 오늘을 1일로 계산하며, 표시된 만료일 23:59:59까지입니다.

체험판
발급 종류를 [Pro 체험판 · 발급 후 3시간]으로 바꾸고 고객이 실제 신청한 이메일을 입력하세요.
같은 이메일 또는 같은 PC의 체험 이력이 있으면 발급이 차단됩니다.
3시간은 첫 실행이 아닌 발급 시점부터입니다. 같은 체험 키를 재입력해도 시간이 늘지 않습니다.
다른 PC와 다른 이메일을 함께 사용하는 동일인은 본인확인 없이 자동 식별할 수 없습니다.
이메일 입력은 신원 확인을 대신하지 않으므로, 실제 수신한 신청 주소를 사용하세요.

추가 등록권
발급 종류를 [Pro 추가 등록권]으로 바꾸고 고객 등록코드·추가 횟수를 입력하세요.
복사한 키를 고객에게 보내면 앱의 라이선스 > Pro 추가 등록권에서 충전합니다.
한 키는 한 번만 충전됩니다. Pro 기본 100회를 먼저 사용하고 추가 횟수를 차감합니다.
추가 등록권도 계정별 50회 제한을 늘리지 않습니다. 가격은 자동 결제하지 않고 이메일로 협의합니다.

키 관리
현재 연결되는 기존 키 보관 위치: C:\\Users\\PUVKID\\Documents\\Lumivolt_AdminVault_20261013
8차 폴더에 별도로 생성된 불일치 발급키는 자동으로 선택하지 않습니다.
관리 메뉴: 기존 보안키 연결, 발급 이력, 암호화 백업/복원.
개인키는 ZIP에 들어 있지 않습니다. PC 이전 전 반드시 암호화 백업과 records/credits 이력을 별도로 보관하세요.
복원한 폴더의 기존 records/credits를 함께 보존해야 체험판 중복 발급 확인도 이어집니다.
TPM·Windows 계정 변경 후 고객 인증은 새 기기 등록이 필요합니다.

라이선스 기간 연장/등급 변경은 같은 등록코드로 새 키를 발급하면 됩니다.
등록코드 유효기간은 30일이므로 오래된 코드는 고객에게 새로 복사해 달라고 해 주세요.
만료일 연장은 기존 남은 기간에 자동 합산하지 않고, 선택한 새 만료일을 적용합니다.
`);
write('SECURITY.md',`# EXP 2.2.1 보안과 운용 범위

- 사용자 앱에는 공개키만 포함합니다. 발급 코드·Ed25519 개인키·개인키 백업은 고객 파일에 없습니다.
- LV4 원본 UTF-8 payload에 Ed25519 서명을 검증합니다. 등급, 기간, 체험 여부, PC 공개키 모두 서명 범위입니다. LV3 기존 키는 Pro로 호환합니다.
- 기기 소유 증명은 Windows Microsoft Platform Crypto Provider의 기존 비이동형 P-256 키를 사용합니다. MachineGuid는 설정 호환/진단용이며 인증 자격이 아닙니다.
- 요청이 'TPM'이라는 문자열을 포함한다는 것만으로 원격 TPM attestation이 이루어지는 것은 아닙니다. 실제 사용 시 플랫폼 키의 공개키 일치와 새 난수 서명을 검증합니다.
- 발급키는 기존 DPAPI 보관소를 사용합니다. 앱과 발급 공개키가 일치해야 발급합니다. 암호화 백업은 PBKDF2-SHA256 + AES-GCM입니다.
- 사용량은 Windows 사용자/기기에 귀속되며 DPAPI 파일·레지스트리 복제본 및 지속 키 표식으로 단순 파일 삭제 초기화를 거부합니다. 계정별 사용량은 상점 번호의 해시로 기록합니다.
- 기본 한도와 추가 등록권은 별도입니다. 날짜가 바뀌면 기본 사용량/계정별 사용량만 0으로 바꾸며 기본 잔여량을 충전하지 않습니다. 추가 등록권 ID를 영구 기록해 재충전을 거부합니다.
- 체험판은 서명된 발급 시각부터 최대 3시간, Pro만 허용됩니다. Master는 records에서 동일 이메일/기기 재발급을 차단하며 클라이언트는 최초 체험 licenseId를 기록합니다.
- 서명·기기·만료는 시작, 주기 검사, 실행 시작, 실제 제출 직전에 검사합니다. Classic의 반복/중복/시간대 예약은 UI 외 실행 경로에서도 차단합니다.
- 실제 제출 직전에 사용량을 확정합니다. 제출 후 결과 미확인, 강제 종료, 통신 실패의 불확실한 결과는 되돌려 주지 않습니다. 폼 준비 단계 실패는 차감하지 않습니다.

## 서버 없는 방식의 한계
중앙 서버 없이 여러 PC의 사람 단위 사용량이나 동일인을 합산/확정할 수 없습니다. 이메일은 관리자가 기록하는 신청 식별자이며 인증된 본인확인 수단이 아닙니다. 다른 PC+다른 이메일의 동일인을 확실하게 막는다고 주장하지 않습니다.
로컬 관리자에 의한 코드 패치, 모든 관련 보호 상태/키 표식 삭제, 전체 머신 스냅샷 롤백까지 완벽하게 차단하는 구조는 아닙니다. TPM 표식은 TPM 하드웨어 단조 증가 카운터가 아닙니다. 소스가 있는 사용자에게 변조가 불가능하다는 보장도 없습니다.
PC/Windows 계정 및 TPM 초기화는 인증 복구/재발급이 필요한 작업입니다. 일반 앱 제거는 사용량·체험 이력·TPM 키·데이터를 삭제하지 않습니다.

## 시각
번개장터 HTTPS 응답 Date(+유효한 Age)를 TLS 검증 후 읽고, Stopwatch로 경과 시간을 계산합니다. 시각은 UTC+9로 변환합니다. 1분 주기로 갱신을 시도하고 30분보다 오래된 동기화 상태에서는 새 등록을 중단합니다.
HTTPS 웹/CDN 응답 시각이며 번개장터 내부 등록 한도 서비스와의 시계 일치는 보증하지 않습니다. 브라우저 준비와 네트워크 처리 시간 때문에 예약 시작과 실제 게시 완료 시각은 다를 수 있습니다.
08:50 등록 후 45분 대기 중 09:00 예약이 있으면 09:00에 다음 실행을 시작합니다. 자정·빈 시간대·중첩 시간대를 처리하며 중첩 시 가장 최근에 시작한 규칙을 우선합니다.

## 빌드
Windows .NET Framework 4.7.2 이상. 3_소스코드/build.bat 또는 build/Build.ps1 사용.
설치 파일: build/Build-Installer.ps1 -ISCC <Inno Setup ISCC.exe 경로>.
공식 도구 문서: https://jrsoftware.org/ishelp/contents.htm
폰트는 Nanum Gothic Bold를 내장하고 OFL을 포함합니다. Inno Setup과 BouncyCastle 고지를 licenses에 포함했습니다.
Authenticode 인증서가 없어 코드 서명은 적용하지 않았습니다.
`);
write('4_검증결과/검증범위.txt',`EXP 2.2.1 검증

암호/TPM 47 + 기존 기능 회귀 110 + 새 등급/체험/사용량/예약 47 + Gate 통합 11 + 실제 배포 EXE 3 = 218 통과.
화면 렌더: 사용자 20개(9페이지+첫 인증, 라이트/다크), Master 6개(정식/체험/추가권, 라이트/다크).
11차 설치 파일 컴파일 성공. 이번 수정은 이메일·화면 변경이며, 실제 설치/제거는 9차에서 검증한 동일한 설치 흐름입니다. 11차에서는 고객의 인증 상태를 바꾸지 않도록 설치/제거를 재실행하지 않았습니다.
Classic/Studio 키 서명 및 설치 등급 코드는 자동 검증했으며 해당 두 등급의 실제 Windows 설치는 별도 실계정 키로 수행하지 않았습니다.
네트워크: 실제 번개장터 HTTPS 시각 동기화 통과.
실제 상품 게시, 로그인한 내 상점 링크의 실제 DOM, 다른 물리 PC, 재부팅, OS 재설치, 여러 DPI/Windows 조합은 미검증입니다.
UI 검증은 테스트 빌드의 빈 데이터에서 수행했고 상품을 게시하지 않았습니다.
보안상 시각/상점 확인에 실패하면 게시를 중단하므로 실계정 인식 확인이 배포 전 남아 있습니다.
`);
console.log(stage);
