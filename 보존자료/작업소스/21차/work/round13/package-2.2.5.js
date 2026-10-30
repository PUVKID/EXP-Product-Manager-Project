const fs=require('fs'),p=require('path'),crypto=require('crypto');
const base=p.resolve('work/round13'),src=p.join(base,'source'),stage=p.join(base,'delivery/EXP_v2.2.5'),out=p.resolve('outputs');
fs.mkdirSync(stage,{recursive:true});fs.mkdirSync(out,{recursive:true});
function tree(a,b){fs.mkdirSync(b,{recursive:true});for(const e of fs.readdirSync(a,{withFileTypes:true})){if(['dist','bin','obj','.git'].includes(e.name))continue;const x=p.join(a,e.name),y=p.join(b,e.name);if(e.isDirectory())tree(x,y);else fs.copyFileSync(x,y);}}
function copy(a,b){fs.mkdirSync(p.dirname(b),{recursive:true});fs.copyFileSync(a,b);}
function write(rel,text){const f=p.join(stage,rel);fs.mkdirSync(p.dirname(f),{recursive:true});fs.writeFileSync(f,'\ufeff'+text);}
tree(src,p.join(stage,'3_소스코드'));
fs.renameSync(p.join(stage,'3_소스코드/Lumivolt.csproj'),p.join(stage,'3_소스코드/EXP.csproj'));
copy(p.resolve('work/round11b/delivery/EXP_v2.2.2/EXP.sln'),p.join(stage,'EXP.sln'));
copy(p.join(src,'dist/EXP.exe'),p.join(stage,'2_윤석님용/설치없이확인/EXP.exe'));
copy(p.join(src,'dist/EXP-Master.exe'),p.join(stage,'2_윤석님용/EXP-Master.exe'));
copy(p.join(src,'dist/installer/EXP_Setup_2.2.5.exe'),p.join(stage,'1_고객배포/EXP_Setup_2.2.5.exe'));
tree(p.join(src,'licenses'),p.join(stage,'1_고객배포/licenses'));
copy(p.join(src,'resources/fonts/NanumGothic-OFL.txt'),p.join(stage,'1_고객배포/licenses/NanumGothic-OFL.txt'));
tree(p.join(stage,'1_고객배포/licenses'),p.join(stage,'2_윤석님용/licenses'));
const prior=p.resolve('work/round11b/delivery/EXP_v2.2.2');
for(const rel of ['1_고객배포/시작하기.txt','2_윤석님용/발급방법.txt','SECURITY.md']){
 let text=fs.readFileSync(p.join(prior,rel),'utf8').replace(/^\ufeff/,'').replaceAll('2.2.2','2.2.5')
 .replace('현재 연결되는 기존 키 보관 위치: C:\\Users\\PUVKID\\Documents\\Lumivolt_AdminVault_20261013','13차 새 발급키 보관 위치: C:\\Users\\PUVKID\\Documents\\EXP_MasterVault_13차_20261013')
 .replace('8차 폴더에 별도로 생성된 불일치 발급키는 자동으로 선택하지 않습니다.','13차는 새 발급 보안키를 사용합니다. 이전 버전의 발급키/인증키와 호환되지 않습니다. 고객에게 13차의 새 등록코드를 받아 새 키를 발급하세요.')
 .replace('LV3 기존 키는 Pro로 호환합니다.','LV3 형식 처리는 남아 있으나 13차의 새 공개키와 일치하는 서명만 인정합니다. 이전 발급키의 인증은 거부합니다.')
 .replace('발급키는 기존 DPAPI 보관소를 사용합니다.','발급키는 13차용으로 새로 생성한 DPAPI 보관소를 사용합니다.');
 write(rel,text);
}
const note="EXP 2.2.5 · 13차 사용량 기록 오류 수정\n\n고객 설치: EXP_Setup_2.2.5.exe\n관리자: EXP-Master.exe\n설치 없이 확인: EXP.exe\n\n사용량 기록의 파일 저장이 연속으로 중단될 때 정상 기록도 되돌리기로 오인하는 결함을 수정했습니다.\n서명된 라이선스의 기기, DPAPI 보호, 기록 순서와 연결 해시를 검증한 다음,\n중단된 저장을 먼저 완료하고 다음 기록을 저장합니다.\n검증되지 않은 기록을 자동 초기화하거나, 더 오래된 사용량으로 돌아가지 않습니다.\n등록 사용량·추가권 잔액·충전 이력·체험 이력을 그대로 보존합니다.\n\n이번 수정은 현재 인증키, 발급 보안키와 상품 6개를 그대로 유지합니다.\n기존 키로 바로 사용할 수 있고 다시 발급할 필요가 없습니다.\n상품 6개는 이전 작업에서 원본으로 복구했으며 과거 등록 성공 이력은 원본에 없습니다.\n약관·저작권·제작자 EXPMASTER, 나눔고딕 Bold, 기존 등급·기간·디자인을 유지합니다.\n고객에게는 고객배포 ZIP이나 설치 EXE만 전달하세요.\n실제 개인 발급키·인증키·상품 정보는 배포 패키지에 들어 있지 않습니다.\n문의: expmasterxx00@gmail.com\n\n검증: 기존 코드에서 동일 경고 재현. 수정본 복구 검사 18개 및 기존 검사 47개 통과.\n실제 상품 등록은 수행하지 않았습니다.\n";
write('먼저읽기.txt',note);
for(const f of ['security-results.txt','regression-results.txt','release-results.txt','gate-results.txt','production-results.txt','admin-ui-results.txt','build-results.txt','rotation-icon-results.txt','reset-results.txt','rebrand-build-results.txt','branding-results.txt','restore-results.txt','recovery-before-results.txt','recovery-results.txt','recovery-release-results.txt','recovery-build-results.txt'])copy(p.join(base,f),p.join(stage,'4_검증결과',f));
for(const f of fs.readdirSync(base).filter(x=>/^master-(Light|Dark)-\d\.png$/.test(x)))copy(p.join(base,f),p.join(stage,'4_검증결과/화면',f));
write('4_검증결과/검증범위.txt',"EXP 2.2.5 검증\n\n기존 2.2.4에서 연속 파일 교체 실패를 주입하여 되돌리기 감지 오류를 재현했습니다.\n수정본의 18개 복구/변조 검사와 47개 라이선스/사용량/예약 회귀 검사가 통과했습니다.\n다른 기기, 비연속 과거 기록, 파일 삭제, 두 사본 삭제, DPAPI 변조 차단을 확인했습니다.\n기본 한도, 추가권, 충전 이력, 체험 이력을 복구 과정에서 유지합니다.\n나머지 첨부된 이전 버전 검증/화면은 기반 기능 검증 자료입니다.\n실제 상품 게시나 다른 물리 PC 검사는 수행하지 않았습니다.\n");
const hashes={};
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const f=p.join(dir,e.name);if(e.isDirectory())walk(f);else{const rel=p.relative(stage,f).replaceAll('\\','/');if(/license_private|\.(dpapi|lvbackup|lvlicense|pfx|p12)$/i.test(rel))throw Error('Secret found '+rel);if(e.name!=='SHA256.json')hashes[rel]=crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}}}
walk(stage);write('SHA256.json',JSON.stringify(hashes,null,2));
for(const [a,b] of [['1_고객배포/EXP_Setup_2.2.5.exe','EXP_Setup_2.2.5.exe'],['2_윤석님용/EXP-Master.exe','EXP-Master_13차.exe'],['2_윤석님용/설치없이확인/EXP.exe','EXP_13차.exe'],['먼저읽기.txt','EXP_13차_먼저읽기.txt']])copy(p.join(stage,a),p.join(out,b));
console.log('Staged '+Object.keys(hashes).length+' files; no private keys or customer data.');
