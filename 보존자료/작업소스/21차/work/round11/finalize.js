const fs=require('fs'),p=require('path'),crypto=require('crypto');
const base=p.resolve('work/round11'),stage=p.join(base,'delivery/EXP_v2.2.1'),out=p.resolve('outputs');
fs.mkdirSync(out,{recursive:true});
const note=`EXP 2.2.1 · 11차 변경 안내

사용자 설치: EXP_Setup_2.2.1.exe
윤석님 관리자: EXP-Master.exe
설치 없이 확인: EXP.exe (동일한 기기 인증 필요)

문의 이메일을 expmasterxx00@gmail.com으로 변경했습니다.
첫 인증, 사용 설명서, 추가 등록권 안내, 설치 프로그램에 반영했습니다.
이메일은 문의 주소로만 사용하며 Gmail 비밀번호나 별도 서버 설정은 필요하지 않습니다.

인증 화면, 대시보드, EXP Master에 푸른 그라데이션과 곡선 그래픽을 적용했습니다.
사이드바 선택 표시와 라이트/다크 색상, 버튼의 부드러운 호버 반응을 다듬었습니다.
나눔고딕 Bold 내장 및 기존 등급·기간·체험판·일일 사용량 정책은 유지합니다.

[파일로 받은 경우]는 유지했습니다. 파일 가져오기와 텍스트 붙여넣기는 동일한 서명·기기 검증을 거칩니다.
키 파일이나 EXE를 복사하는 것만으로 다른 PC에서 인증되지 않습니다.
다른 기기용으로 서명된 키가 거부되는 테스트를 통과했습니다.
로컬 관리자에 의한 프로그램 변조까지 완벽하게 방지한다는 의미는 아닙니다.

고객에게는 EXP_v2.2.1_고객배포.zip 또는 EXP_Setup_2.2.1.exe만 전달하세요.
전체 ZIP에는 관리자 도구와 소스가 포함됩니다. 개인 발급키는 어느 ZIP에도 넣지 않았습니다.
기존에 발급한 인증키도 계속 사용할 수 있습니다. 이번 업데이트는 인증 상태를 초기화하지 않습니다.

검증: 자동 검사 218개 통과, 사용자 20개 화면 및 Master 6개 화면 렌더 확인, 설치 파일 빌드 성공.
실제 번개장터 상품 게시 및 다른 물리 PC에서의 실행은 이번에 수행하지 않았습니다.
`;
fs.writeFileSync(p.join(stage,'11차_변경사항.txt'),'\ufeff'+note);
const read=p.join(stage,'먼저읽기.txt');fs.writeFileSync(read,'\ufeff'+note+'\n'+fs.readFileSync(read,'utf8').replace(/^\ufeff/,''));
function copy(a,b){fs.copyFileSync(p.join(stage,a),p.join(out,b));}
copy('1_고객배포/EXP_Setup_2.2.1.exe','EXP_Setup_2.2.1.exe');
copy('2_윤석님용/EXP-Master.exe','EXP-Master.exe');
copy('2_윤석님용/설치없이확인/EXP.exe','EXP.exe');
copy('먼저읽기.txt','EXP_11차_먼저읽기.txt');
fs.copyFileSync(p.join(base,'ui/Light-dashboard.png'),p.join(out,'EXP_사용자_라이트.png'));
fs.copyFileSync(p.join(base,'master-Dark-0.png'),p.join(out,'EXP_Master_다크.png'));
let hashes={};function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){let f=p.join(dir,e.name);if(e.isDirectory())walk(f);else if(e.name!=='SHA256.json')hashes[p.relative(stage,f).replaceAll('\\','/')]=crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}}walk(stage);
fs.writeFileSync(p.join(stage,'SHA256.json'),JSON.stringify(hashes,null,2));
console.log('Staged '+Object.keys(hashes).length+' files; outputs ready.');
