const fs=require('fs'),p=require('path');const base=p.resolve('work/round13');
let script=fs.readFileSync(p.join(base,'package-2.2.4.js'),'utf8').replaceAll('2.2.4','2.2.5');
const note=`EXP 2.2.5 · 13차 사용량 기록 오류 수정

고객 설치: EXP_Setup_2.2.5.exe
관리자: EXP-Master.exe
설치 없이 확인: EXP.exe

사용량 기록의 파일 저장이 연속으로 중단될 때 정상 기록도 되돌리기로 오인하는 결함을 수정했습니다.
서명된 라이선스의 기기, DPAPI 보호, 기록 순서와 연결 해시를 검증한 다음,
중단된 저장을 먼저 완료하고 다음 기록을 저장합니다.
검증되지 않은 기록을 자동 초기화하거나, 더 오래된 사용량으로 돌아가지 않습니다.
등록 사용량·추가권 잔액·충전 이력·체험 이력을 그대로 보존합니다.

이번 수정은 현재 인증키, 발급 보안키와 상품 6개를 그대로 유지합니다.
기존 키로 바로 사용할 수 있고 다시 발급할 필요가 없습니다.
상품 6개는 이전 작업에서 원본으로 복구했으며 과거 등록 성공 이력은 원본에 없습니다.
약관·저작권·제작자 EXPMASTER, 나눔고딕 Bold, 기존 등급·기간·디자인을 유지합니다.
고객에게는 고객배포 ZIP이나 설치 EXE만 전달하세요.
실제 개인 발급키·인증키·상품 정보는 배포 패키지에 들어 있지 않습니다.
문의: expmasterxx00@gmail.com

검증: 기존 코드에서 동일 경고 재현. 수정본 복구 검사 18개 및 기존 검사 47개 통과.
실제 상품 등록은 수행하지 않았습니다.
`;
const begin=script.indexOf('const note=`'),end=script.indexOf('write(\'먼저읽기.txt\',note);',begin);
if(begin<0||end<0)throw Error('Package template not found');
script=script.slice(0,begin)+'const note='+JSON.stringify(note)+';\n'+script.slice(end);
script=script.replace("'branding-results.txt','restore-results.txt'","'branding-results.txt','restore-results.txt','recovery-before-results.txt','recovery-results.txt','recovery-release-results.txt','recovery-build-results.txt'");
const vstart=script.indexOf("write('4_검증결과/검증범위.txt',"),vend=script.indexOf('const hashes={};',vstart);
const verification='EXP 2.2.5 검증\n\n기존 2.2.4에서 연속 파일 교체 실패를 주입하여 되돌리기 감지 오류를 재현했습니다.\n수정본의 18개 복구/변조 검사와 47개 라이선스/사용량/예약 회귀 검사가 통과했습니다.\n다른 기기, 비연속 과거 기록, 파일 삭제, 두 사본 삭제, DPAPI 변조 차단을 확인했습니다.\n기본 한도, 추가권, 충전 이력, 체험 이력을 복구 과정에서 유지합니다.\n나머지 첨부된 이전 버전 검증/화면은 기반 기능 검증 자료입니다.\n실제 상품 게시나 다른 물리 PC 검사는 수행하지 않았습니다.\n';
script=script.slice(0,vstart)+"write('4_검증결과/검증범위.txt',"+JSON.stringify(verification)+");\n"+script.slice(vend);
fs.writeFileSync(p.join(base,'package-2.2.5.js'),script);
