const fs=require('fs'),p=require('path'),crypto=require('crypto');
const base=p.resolve('work/round13'),src=p.join(base,'source'),stage=p.join(base,'delivery/EXP_v2.2.6'),out=p.resolve('outputs');
fs.mkdirSync(stage,{recursive:true});fs.mkdirSync(out,{recursive:true});
function tree(a,b){fs.mkdirSync(b,{recursive:true});for(const e of fs.readdirSync(a,{withFileTypes:true})){if(['dist','bin','obj','.git'].includes(e.name))continue;const x=p.join(a,e.name),y=p.join(b,e.name);if(e.isDirectory())tree(x,y);else fs.copyFileSync(x,y);}}
function copy(a,b){fs.mkdirSync(p.dirname(b),{recursive:true});fs.copyFileSync(a,b);}
function write(rel,text){const f=p.join(stage,rel);fs.mkdirSync(p.dirname(f),{recursive:true});fs.writeFileSync(f,'\ufeff'+text);}
tree(src,p.join(stage,'3_소스코드'));
fs.renameSync(p.join(stage,'3_소스코드/Lumivolt.csproj'),p.join(stage,'3_소스코드/EXP.csproj'));
copy(p.resolve('work/round11b/delivery/EXP_v2.2.2/EXP.sln'),p.join(stage,'EXP.sln'));
copy(p.join(src,'dist/EXP.exe'),p.join(stage,'2_윤석님용/설치없이확인/EXP.exe'));
copy(p.join(src,'dist/EXP-Master.exe'),p.join(stage,'2_윤석님용/EXP-Master.exe'));
copy(p.join(src,'dist/installer/EXP_Setup_2.2.6.exe'),p.join(stage,'1_고객배포/EXP_Setup_2.2.6.exe'));
tree(p.join(src,'licenses'),p.join(stage,'1_고객배포/licenses'));
copy(p.join(src,'resources/fonts/NanumGothic-OFL.txt'),p.join(stage,'1_고객배포/licenses/NanumGothic-OFL.txt'));
tree(p.join(stage,'1_고객배포/licenses'),p.join(stage,'2_윤석님용/licenses'));
const prior=p.resolve('work/round11b/delivery/EXP_v2.2.2');
for(const rel of ['1_고객배포/시작하기.txt','2_윤석님용/발급방법.txt','SECURITY.md']){
 let text=fs.readFileSync(p.join(prior,rel),'utf8').replace(/^\ufeff/,'').replaceAll('2.2.2','2.2.6')
 .replace('현재 연결되는 기존 키 보관 위치: C:\\Users\\PUVKID\\Documents\\Lumivolt_AdminVault_20261013','13차 새 발급키 보관 위치: C:\\Users\\PUVKID\\Documents\\EXP_MasterVault_13차_20261013')
 .replace('8차 폴더에 별도로 생성된 불일치 발급키는 자동으로 선택하지 않습니다.','13차는 새 발급 보안키를 사용합니다. 이전 버전의 발급키/인증키와 호환되지 않습니다. 고객에게 13차의 새 등록코드를 받아 새 키를 발급하세요.')
 .replace('LV3 기존 키는 Pro로 호환합니다.','LV3 형식 처리는 남아 있으나 13차의 새 공개키와 일치하는 서명만 인정합니다. 이전 발급키의 인증은 거부합니다.')
 .replace('발급키는 기존 DPAPI 보관소를 사용합니다.','발급키는 13차용으로 새로 생성한 DPAPI 보관소를 사용합니다.');
 write(rel,text);
}
const note="EXP 2.2.6 · 13차 디자인 수정 / 직접 설치용\n\n윤석님이 직접 실행할 설치 파일: EXP_Setup_2.2.6.exe\n발급 관리자: EXP-Master.exe\n\n이번 버전은 설치하지 않았습니다. 이전 설치와 바로가기를 제거했습니다.\n상품 목록 6개와 사진만 유지하고 사용자 인증·체험·사용량·로그인·설정·약관 동의를 초기화했습니다.\n관리자 발급 이력·추가권 이력·설정도 초기화했습니다.\n13차 발급용 보안키는 유지됩니다. 새 설치 화면에서 등록코드를 만들고 EXP Master에서 새 인증키를 발급하세요.\n초기화 과정에서 PC 인증 키가 삭제되었으므로 이전 등록코드/인증키 대신 새 코드를 사용하세요.\n발급 보안키는 Documents/EXP_MasterVault_13차_20261013에 있으며 배포 ZIP에는 포함되지 않습니다.\n\n디자인 변경\n- 검색·Chrome 경로·라이선스 키·관리자 입력칸: 둥근 배경, 내부 여백, 포커스 표시.\n- 숫자 입력: − / + 버튼과 직접 입력을 함께 지원.\n- 상품 표: 왼쪽 빈 행 머리글 제거, 선을 줄이고 행 배경으로 구분. 상품을 두 번 누르면 편집.\n- 선택 메뉴·날짜 선택기 및 설명서 스크롤 스타일 정리.\n- 중복 실행·오류·확인 안내를 앱과 같은 테마의 안내창으로 변경.\n- 나눔고딕 Bold와 EXPMASTER 표기, 라이트/다크 유지.\n\n설치와 인증은 윤석님이 직접 진행하세요. 상품 데이터는 이 PC에 그대로 있습니다.\n고객에게는 고객배포 ZIP 또는 설치 EXE만 전달하세요.\n문의: expmasterxx00@gmail.com\n";
write('먼저읽기.txt',note);
for(const f of ['security-results.txt','regression-results.txt','release-results.txt','gate-results.txt','production-results.txt','admin-ui-results.txt','build-results.txt','rotation-icon-results.txt','reset-results.txt','rebrand-build-results.txt','branding-results.txt','restore-results.txt','recovery-before-results.txt','recovery-results.txt','recovery-release-results.txt','recovery-build-results.txt','surfaces-ui-results.txt','surfaces-master-results.txt','surfaces-regression-results.txt','surfaces-revision-results.txt','surfaces-controls-results.txt','surfaces-controls-125-results.txt','surfaces-installer-build.txt','surfaces-reset-results.txt'])copy(p.join(base,f),p.join(stage,'4_검증결과',f));
for(const f of fs.readdirSync(base).filter(x=>/^surfaces-master-/.test(x)&&x.endsWith('.png')))copy(p.join(base,f),p.join(stage,'4_검증결과/화면',f));
for(const dir of ['surfaces-ui','surfaces-revision','surfaces-controls','surfaces-controls-125'])for(const f of fs.readdirSync(p.join(base,dir)))copy(p.join(base,dir,f),p.join(stage,'4_검증결과/화면',dir+'-'+f));
write('4_검증결과/검증범위.txt',"EXP 2.2.6 검증\n\n기존 기능 회귀 91개, 상품 편집/진단 등 22개 통과.\n새 입력/숫자/알림창 동작 16개 통과, 125% 모의 배율에서 동일 16개 통과.\n사용자 18화면과 관리자 6화면, 수정 화면 및 안내창을 라이트/다크로 렌더해 확인했습니다.\n설치 파일은 컴파일만 했으며 사용자 요청에 따라 새 설치 및 인증을 수행하지 않았습니다.\n이전 설치는 제거했습니다. 상품 목록 6개와 사진 보존, 사용자/관리자 상태 초기화 완료.\n기존 사용량 저장 복구 수정은 유지합니다. 첨부한 이전 버전 검증은 기반 기능 자료입니다.\n");
const hashes={};
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const f=p.join(dir,e.name);if(e.isDirectory())walk(f);else{const rel=p.relative(stage,f).replaceAll('\\','/');if(/license_private|\.(dpapi|lvbackup|lvlicense|pfx|p12)$/i.test(rel))throw Error('Secret found '+rel);if(e.name!=='SHA256.json')hashes[rel]=crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}}}
walk(stage);write('SHA256.json',JSON.stringify(hashes,null,2));
for(const [a,b] of [['1_고객배포/EXP_Setup_2.2.6.exe','EXP_Setup_2.2.6.exe'],['2_윤석님용/EXP-Master.exe','EXP-Master_13차.exe'],['2_윤석님용/설치없이확인/EXP.exe','EXP_13차.exe'],['먼저읽기.txt','EXP_13차_먼저읽기.txt']])copy(p.join(stage,a),p.join(out,b));
console.log('Staged '+Object.keys(hashes).length+' files; no private keys or customer data.');
