const fs=require('fs'),p=require('path');const base=p.resolve('work/round13');
let script=fs.readFileSync(p.join(base,'package-2.2.5.js'),'utf8').replaceAll('2.2.5','2.2.6');
const note=`EXP 2.2.6 · 13차 디자인 수정 / 직접 설치용

윤석님이 직접 실행할 설치 파일: EXP_Setup_2.2.6.exe
발급 관리자: EXP-Master.exe

이번 버전은 설치하지 않았습니다. 이전 설치와 바로가기를 제거했습니다.
상품 목록 6개와 사진만 유지하고 사용자 인증·체험·사용량·로그인·설정·약관 동의를 초기화했습니다.
관리자 발급 이력·추가권 이력·설정도 초기화했습니다.
13차 발급용 보안키는 유지됩니다. 새 설치 화면에서 등록코드를 만들고 EXP Master에서 새 인증키를 발급하세요.
초기화 과정에서 PC 인증 키가 삭제되었으므로 이전 등록코드/인증키 대신 새 코드를 사용하세요.
발급 보안키는 Documents/EXP_MasterVault_13차_20261013에 있으며 배포 ZIP에는 포함되지 않습니다.

디자인 변경
- 검색·Chrome 경로·라이선스 키·관리자 입력칸: 둥근 배경, 내부 여백, 포커스 표시.
- 숫자 입력: − / + 버튼과 직접 입력을 함께 지원.
- 상품 표: 왼쪽 빈 행 머리글 제거, 선을 줄이고 행 배경으로 구분. 상품을 두 번 누르면 편집.
- 선택 메뉴·날짜 선택기 및 설명서 스크롤 스타일 정리.
- 중복 실행·오류·확인 안내를 앱과 같은 테마의 안내창으로 변경.
- 나눔고딕 Bold와 EXPMASTER 표기, 라이트/다크 유지.

설치와 인증은 윤석님이 직접 진행하세요. 상품 데이터는 이 PC에 그대로 있습니다.
고객에게는 고객배포 ZIP 또는 설치 EXE만 전달하세요.
문의: expmasterxx00@gmail.com
`;
const start=script.indexOf('const note='),end=script.indexOf("write('먼저읽기.txt',note);",start);script=script.slice(0,start)+'const note='+JSON.stringify(note)+';\n'+script.slice(end);
const vstart=script.indexOf("write('4_검증결과/검증범위.txt',"),vend=script.indexOf('const hashes={};',vstart);
const report='EXP 2.2.6 검증\n\n기존 기능 회귀 91개, 상품 편집/진단 등 22개 통과.\n새 입력/숫자/알림창 동작 16개 통과, 125% 모의 배율에서 동일 16개 통과.\n사용자 18화면과 관리자 6화면, 수정 화면 및 안내창을 라이트/다크로 렌더해 확인했습니다.\n설치 파일은 컴파일만 했으며 사용자 요청에 따라 새 설치 및 인증을 수행하지 않았습니다.\n이전 설치는 제거했습니다. 상품 목록 6개와 사진 보존, 사용자/관리자 상태 초기화 완료.\n기존 사용량 저장 복구 수정은 유지합니다. 첨부한 이전 버전 검증은 기반 기능 자료입니다.\n';
script=script.slice(0,vstart)+"write('4_검증결과/검증범위.txt',"+JSON.stringify(report)+");\n"+script.slice(vend);
script=script.replace("for(const f of fs.readdirSync(base).filter(x=>/^master-(Light|Dark)-\\d\\.png$/.test(x)))copy(p.join(base,f),p.join(stage,'4_검증결과/화면',f));", "for(const f of fs.readdirSync(base).filter(x=>/^surfaces-master-/.test(x)&&x.endsWith('.png')))copy(p.join(base,f),p.join(stage,'4_검증결과/화면',f));\nfor(const dir of ['surfaces-ui','surfaces-revision','surfaces-controls','surfaces-controls-125'])for(const f of fs.readdirSync(p.join(base,dir)))copy(p.join(base,dir,f),p.join(stage,'4_검증결과/화면',dir+'-'+f));");
script=script.replace("'recovery-build-results.txt'","'recovery-build-results.txt','surfaces-ui-results.txt','surfaces-master-results.txt','surfaces-regression-results.txt','surfaces-revision-results.txt','surfaces-controls-results.txt','surfaces-controls-125-results.txt','surfaces-installer-build.txt','surfaces-reset-results.txt'");
fs.writeFileSync(p.join(base,'package-2.2.6.js'),script);
