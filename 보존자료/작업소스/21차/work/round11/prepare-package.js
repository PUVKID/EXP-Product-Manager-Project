const fs=require('fs'),p=require('path');
let s=fs.readFileSync('work/round9/package.js','utf8');
s=s.replace("const base=p.resolve('work/round9'),src=p.join(base,'Lumivolt_v2.2.0/3_소스코드'),stage=p.join(base,'delivery/EXP_v2.2.0');","const base=p.resolve('work/round11'),src=p.join(base,'source'),stage=p.join(base,'delivery/EXP_v2.2.1');");
s=s.replace("p.join(base,'Lumivolt_v2.2.0/Lumivolt.sln')","p.resolve('work/round9/Lumivolt_v2.2.0/Lumivolt.sln')");
s=s.replaceAll('lukeyun@kyonggi.ac.kr','expmasterxx00@gmail.com').replaceAll('2.2.0','2.2.1').replaceAll('9차','11차');
// The retained source solution is still at its original path.
s=s.replace("work/round9/Lumivolt_v2.2.1/Lumivolt.sln","work/round9/Lumivolt_v2.2.0/Lumivolt.sln");
s=s.replace("'ui-results.txt','uninstall-results.json','installer-results.json'","'ui-results.txt','admin-ui-results.txt','build-results.txt'");
s=s.replace('실제 설치 파일: 현재 유효한 PC 귀속 라이선스로 Pro 자동 선택 설치, 제거, 데이터/키/사용량 보존 확인.','11차 설치 파일 컴파일 성공. 이번 수정은 이메일·화면 변경이며, 실제 설치/제거는 9차에서 검증한 동일한 설치 흐름입니다. 11차에서는 고객의 인증 상태를 바꾸지 않도록 설치/제거를 재실행하지 않았습니다.');
s=s.replace('EXP 2.2.1 · 11차\r\n','EXP 2.2.1 · 11차\r\n');
fs.writeFileSync('work/round11/package.js',s);
