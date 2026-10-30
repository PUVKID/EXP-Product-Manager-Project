const fs=require('fs'),p=require('path'),crypto=require('crypto');
const base=p.resolve('work/round13'),src=p.join(base,'source'),stage=p.join(base,'delivery/EXP_v2.2.3'),out=p.resolve('outputs');
fs.mkdirSync(stage,{recursive:true});fs.mkdirSync(out,{recursive:true});
function tree(a,b){fs.mkdirSync(b,{recursive:true});for(const e of fs.readdirSync(a,{withFileTypes:true})){if(['dist','bin','obj','.git'].includes(e.name))continue;const x=p.join(a,e.name),y=p.join(b,e.name);if(e.isDirectory())tree(x,y);else fs.copyFileSync(x,y);}}
function copy(a,b){fs.mkdirSync(p.dirname(b),{recursive:true});fs.copyFileSync(a,b);}
function write(rel,text){const f=p.join(stage,rel);fs.mkdirSync(p.dirname(f),{recursive:true});fs.writeFileSync(f,'\ufeff'+text);}
tree(src,p.join(stage,'3_소스코드'));
fs.renameSync(p.join(stage,'3_소스코드/Lumivolt.csproj'),p.join(stage,'3_소스코드/EXP.csproj'));
copy(p.resolve('work/round11b/delivery/EXP_v2.2.2/EXP.sln'),p.join(stage,'EXP.sln'));
copy(p.join(src,'dist/EXP.exe'),p.join(stage,'2_윤석님용/설치없이확인/EXP.exe'));
copy(p.join(src,'dist/EXP-Master.exe'),p.join(stage,'2_윤석님용/EXP-Master.exe'));
copy(p.join(src,'dist/installer/EXP_Setup_2.2.3.exe'),p.join(stage,'1_고객배포/EXP_Setup_2.2.3.exe'));
tree(p.join(src,'licenses'),p.join(stage,'1_고객배포/licenses'));
copy(p.join(src,'resources/fonts/NanumGothic-OFL.txt'),p.join(stage,'1_고객배포/licenses/NanumGothic-OFL.txt'));
tree(p.join(stage,'1_고객배포/licenses'),p.join(stage,'2_윤석님용/licenses'));
const prior=p.resolve('work/round11b/delivery/EXP_v2.2.2');
for(const rel of ['1_고객배포/시작하기.txt','2_윤석님용/발급방법.txt','SECURITY.md']){
 let text=fs.readFileSync(p.join(prior,rel),'utf8').replace(/^\ufeff/,'').replaceAll('2.2.2','2.2.3')
 .replace('현재 연결되는 기존 키 보관 위치: C:\\Users\\PUVKID\\Documents\\Lumivolt_AdminVault_20261013','13차 새 발급키 보관 위치: C:\\Users\\PUVKID\\Documents\\EXP_MasterVault_13차_20261013')
 .replace('8차 폴더에 별도로 생성된 불일치 발급키는 자동으로 선택하지 않습니다.','13차는 새 발급 보안키를 사용합니다. 이전 버전의 발급키/인증키와 호환되지 않습니다. 고객에게 13차의 새 등록코드를 받아 새 키를 발급하세요.')
 .replace('LV3 기존 키는 Pro로 호환합니다.','LV3 형식 처리는 남아 있으나 13차의 새 공개키와 일치하는 서명만 인정합니다. 이전 발급키의 인증은 거부합니다.')
 .replace('발급키는 기존 DPAPI 보관소를 사용합니다.','발급키는 13차용으로 새로 생성한 DPAPI 보관소를 사용합니다.');
 write(rel,text);
}
const note=`EXP 2.2.3 · 13차

고객 설치: EXP_Setup_2.2.3.exe
윤석님 관리자: EXP-Master.exe
설치 없이 확인: EXP.exe

EXP Master 창과 대화상자의 아이콘을 EXP 아이콘으로 통일했습니다.
13차부터 새 발급 보안키를 사용합니다. 이전 버전에서 받은 인증키는 사용할 수 없습니다.
고객은 13차 실행 화면에서 등록코드를 복사하고, 윤석님은 13차 EXP Master에서 새 인증키를 발급하세요.
관리자 발급키는 이 PC의 Documents/EXP_MasterVault_13차_20261013에 별도로 보관합니다.
개인 발급키와 실제 고객 데이터는 배포 ZIP에 포함하지 않습니다.

Classic 25개/일, Studio 50개/일과 반복·예약, Pro 100개/일과 추가 등록권.
Pro 체험판은 발급부터 3시간이며 기간제·무기한 발급은 유지합니다.
나눔고딕 Bold, 라이트/다크 및 12차의 상품 편집·대시보드·자가진단 디자인을 유지합니다.
문의: expmasterxx00@gmail.com

이번 PC 초기화
인증·체험·사용량·전용 브라우저 로그인·설정과 관리자 발급 이력을 초기화했습니다.
이전 상태는 배포 폴더 밖에 비공개 보관했습니다. 13차 관리자 이력은 비어 있습니다.
상품 파일은 초기화 직전에 이미 빈 목록이었으며, 윤석님 요청대로 복구하거나 변경하지 않았습니다.
사진과 기타 보존 파일은 해시 비교로 변경되지 않았음을 확인했습니다.
처음부터 다시 인증하려면 이전 버전 대신 이 폴더의 13차를 실행하세요.
이 초기화는 개발 PC에 별도로 수행한 작업입니다. 설치 파일이 고객 정보를 자동 초기화하지 않습니다.

고객에게는 고객배포 ZIP 또는 설치 EXE만 전달하세요. 전체 ZIP에는 관리자 도구와 소스가 있습니다.
실제 번개장터 상품 게시, 다른 PC 및 이번 설치 파일의 설치·제거는 검증하지 않았습니다.
`;
write('먼저읽기.txt',note);
for(const f of ['security-results.txt','regression-results.txt','release-results.txt','gate-results.txt','production-results.txt','admin-ui-results.txt','build-results.txt','rotation-icon-results.txt','reset-results.txt'])copy(p.join(base,f),p.join(stage,'4_검증결과',f));
for(const f of fs.readdirSync(base).filter(x=>/^master-(Light|Dark)-\d\.png$/.test(x)))copy(p.join(base,f),p.join(stage,'4_검증결과/화면',f));
write('4_검증결과/검증범위.txt',`EXP 2.2.3 검증\n\n암호/TPM 47 + 기존 회귀 91 + 등급/체험/한도/예약 47 + 인증 통합 11 + 배포 EXE 4 + 발급키 교체/아이콘 2 = 202 통과.\nMaster 라이트/다크 6화면을 렌더했습니다.\n사용자 화면 변경은 없으며 12차 검증 결과를 유지합니다.\n설치 파일 컴파일 성공. 실제 설치/제거 및 실제 상품 게시는 수행하지 않았습니다.\n인증 초기화 후 앱 전용 TPM 키 2개가 없는 것을 읽기 전용으로 확인했습니다.\n상품 파일은 초기화 전에 이미 빈 배열이었고, 사용자 요청에 따라 복구하지 않았습니다.\n보존한 22개 파일은 초기화 전후 해시가 같습니다.\n`);
const hashes={};
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const f=p.join(dir,e.name);if(e.isDirectory())walk(f);else{const rel=p.relative(stage,f).replaceAll('\\','/');if(/license_private|\.(dpapi|lvbackup|lvlicense|pfx|p12)$/i.test(rel))throw Error('Secret found '+rel);if(e.name!=='SHA256.json')hashes[rel]=crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}}}
walk(stage);write('SHA256.json',JSON.stringify(hashes,null,2));
for(const [a,b] of [['1_고객배포/EXP_Setup_2.2.3.exe','EXP_Setup_2.2.3.exe'],['2_윤석님용/EXP-Master.exe','EXP-Master_13차.exe'],['2_윤석님용/설치없이확인/EXP.exe','EXP_13차.exe'],['먼저읽기.txt','EXP_13차_먼저읽기.txt']])copy(p.join(stage,a),p.join(out,b));
console.log('Staged '+Object.keys(hashes).length+' files; no private keys or customer data.');
