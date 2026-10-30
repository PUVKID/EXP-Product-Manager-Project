const fs=require('fs'),p=require('path'),crypto=require('crypto');
const base=p.resolve('work/round11b'),stage=p.join(base,'delivery/EXP_v2.2.2'),out=p.resolve('outputs');
fs.mkdirSync(out,{recursive:true});
const note=`EXP 2.2.2 · 11차 업데이트

고객 설치: EXP_Setup_2.2.2.exe
윤석님 관리자: EXP-Master.exe
설치 없이 확인: EXP.exe (동일한 인증 필요)

이번 변경
- 상품 편집: 기본 정보 / 설명·사진의 2열 구성, 둥근 입력칸과 부드러운 버튼 반응, 사진 썸네일.
- 한눈에 보기: 한글 값의 크기와 설명 간격 조정, 최근 활동을 가로 전체 너비로 배치.
- 최근 로그 카드와 로그 메뉴, 로그 폴더 열기 제거.
- 자가진단: 기기 인증·라이선스·Chrome·데이터 저장·사진·서버 시각 6개 항목.
  초록 원 PASS, 빨강 원 FAIL, 회색 미검사 및 항목별 해결 안내.
- 운영로그 전송 코드, 대기열, 서버 관리자 로그인과 시스템 정보 수집 코드 제거.
- 사용 설명서를 현재 화면에 맞춰 12개 항목으로 다시 작성.
- 불필요한 안내 문구 정리. 변경된 데이터 처리 방식에 맞춰 약관 안내 수정.

나눔고딕 Bold와 라이트/다크, 기존 인증키·등급·기간·체험판·일일 한도 정책은 유지합니다.
등록과 서버 시각 확인에 필요한 연결은 계속 사용합니다.
상품·설정·최근 활동 및 로컬 오류 기록은 PC에 보관하며 관리자 서버로 전송하지 않습니다.
이 업데이트는 고객 데이터나 인증 상태를 초기화하지 않습니다.

고객에게는 고객배포 ZIP 또는 설치 EXE만 전달하세요. 전체 ZIP은 관리자 도구와 소스를 포함합니다.
개인 발급키는 패키지에 포함하지 않았습니다.
문의: expmasterxx00@gmail.com

검증: 핵심 검사 222개 통과. 추가 125% 배율 검사 22개 통과.
라이트/다크 화면과 상품 저장·취소, 사진 오류 감지, 실제 자가진단 결과를 확인했습니다.
배포 EXE에 로그 전송·서버 관리자·로그 화면 코드가 없는 것을 확인했습니다.
실제 번개장터 상품 게시, 다른 물리 PC, 이번 설치 파일의 설치·제거는 수행하지 않았습니다.
`;
fs.writeFileSync(p.join(stage,'먼저읽기.txt'),'\ufeff'+note);
fs.writeFileSync(p.join(stage,'11차_변경사항.txt'),'\ufeff'+note);
const verification=`EXP 2.2.2 검증 범위

암호/TPM 47 + 회귀 91 + 등급/체험/한도/예약 47 + 인증 통합 11 + 배포 EXE 4 + 수정 기능/화면 22 = 222 통과.
동일한 수정 기능/화면 검사 22개를 125% 모의 배율에서도 통과했습니다.
로그 전송 관련 기존 검사 19개는 해당 기능 제거와 함께 제외했습니다.
사용자 18화면, 관리자 6화면, 수정 화면 10개 × 2배율 = 총 44개 렌더.
모의 배율 검사는 실제 다중 모니터/Windows 디스플레이 배율 전환 검사를 대신하지 않습니다.
실제 제품 등록 및 다른 물리 PC에서 실행하지 않았습니다.
설치 파일 빌드 성공. 설치/제거의 기본 흐름은 이전 버전에서 확인했으며 이번 버전으로 재설치하지 않았습니다.
테스트는 별도 임시 상품/설정을 사용합니다. 테스트 화면의 Pro·무기한·상품 수는 테스트용 예시입니다.
자가진단 화면의 라이선스 FAIL은 테스트 데이터 폴더에 인증키가 없는 실제 결과입니다.
`;
fs.writeFileSync(p.join(stage,'4_검증결과/검증범위.txt'),'\ufeff'+verification);
for(const f of ['revision-results.txt','revision-dpi-results.txt'])fs.copyFileSync(p.join(base,f),p.join(stage,'4_검증결과',f));
for(const dir of ['revision-ui','revision-ui-125'])for(const f of fs.readdirSync(p.join(base,dir)))fs.copyFileSync(p.join(base,dir,f),p.join(stage,'4_검증결과/화면',dir+'-'+f));
function copy(a,b){fs.copyFileSync(p.join(stage,a),p.join(out,b));}
copy('1_고객배포/EXP_Setup_2.2.2.exe','EXP_Setup_2.2.2.exe');copy('2_윤석님용/EXP-Master.exe','EXP-Master.exe');copy('2_윤석님용/설치없이확인/EXP.exe','EXP.exe');copy('먼저읽기.txt','EXP_11차_먼저읽기.txt');
for(const [a,b] of [['revision-ui/Light-dashboard.png','EXP_사용자_라이트.png'],['revision-ui/Dark-product-editor.png','EXP_상품편집.png'],['revision-ui/Dark-diagnostics.png','EXP_자가진단.png'],['master-Dark-0.png','EXP_Master_다크.png']])fs.copyFileSync(p.join(base,a),p.join(out,b));
let hashes={};function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){let f=p.join(dir,e.name);if(e.isDirectory())walk(f);else if(e.name!=='SHA256.json')hashes[p.relative(stage,f).replaceAll('\\','/')]=crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}}walk(stage);
fs.writeFileSync(p.join(stage,'SHA256.json'),JSON.stringify(hashes,null,2));console.log('Staged '+Object.keys(hashes).length+' files.');
