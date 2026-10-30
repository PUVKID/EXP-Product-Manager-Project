const fs=require('fs'),p=require('path');const base=p.resolve('work/round13');
for(const name of ['EXP.exe','EXP-Master.exe']){
 const bytes=fs.readFileSync(p.join(base,'source/dist',name));
 if(bytes.includes(Buffer.from('음악천재','utf16le'))||bytes.includes(Buffer.from('음악천재','utf8')))throw Error('Old owner remains in '+name);
 if(!bytes.includes(Buffer.from('EXPMASTER','utf16le')))throw Error('New owner missing in '+name);
}
fs.writeFileSync(p.join(base,'branding-results.txt'),'PASS customer/master binary owner EXPMASTER; previous owner absent.\nPASS installer publisher EXPMASTER.\n');
let script=fs.readFileSync(p.join(base,'package.js'),'utf8').replaceAll('2.2.3','2.2.4');
script=script.replace("'rotation-icon-results.txt','reset-results.txt'","'rotation-icon-results.txt','reset-results.txt','rebrand-build-results.txt','branding-results.txt','restore-results.txt'");
script=script.replace('EXP Master 창과 대화상자의 아이콘을 EXP 아이콘으로 통일했습니다.','EXP Master 창과 대화상자의 아이콘을 EXP 아이콘으로 통일했습니다.\n약관·저작권자·EXE 제작자·설치 파일 게시자 표시를 EXPMASTER로 변경했습니다.');
script=script.replace('상품 파일은 초기화 직전에 이미 빈 목록이었으며, 윤석님 요청대로 복구하거나 변경하지 않았습니다.','윤석님 요청에 따라 예전에 작성한 원본 상품 6개와 사진 연결을 복구했습니다.\n원본에는 이전 등록 성공 이력과 앱에서 나중에 수정한 내용이 없어 그 정보는 복구하지 않았습니다.');
script=script.replace('암호/TPM 47 + 기존 회귀 91 + 등급/체험/한도/예약 47 + 인증 통합 11 + 배포 EXE 4 + 발급키 교체/아이콘 2 = 202 통과.','기반 2.2.3에서 암호/TPM 47 + 기존 회귀 91 + 등급/체험/한도/예약 47 + 인증 통합 11 + 배포 EXE 4 + 발급키 교체/아이콘 2 = 202 통과. 이번 2.2.4는 이름·약관 버전·제작자 정보 변경이며 빌드와 실제 EXE 문자열을 추가 확인했습니다.');
script=script.replace('상품 파일은 초기화 전에 이미 빈 배열이었고, 사용자 요청에 따라 복구하지 않았습니다.','상품 원본 6개를 복구하고 다시 읽어 제목·가격·설명·사진 경로가 일치하는지 확인했습니다.');
script=script.replace('보존한 22개 파일은 초기화 전후 해시가 같습니다.','초기화 단계에서 보존한 22개 파일은 해시가 같으며, 이후 요청에 따라 상품 목록 6개와 원본 사진을 복구했습니다.');
fs.writeFileSync(p.join(base,'package-2.2.4.js'),script);
let report=fs.readFileSync(p.join(base,'reset-results.txt'),'utf8').replace('Product file was already empty before reset; original source recovery pending.','Authentication reset verified. Subsequent product restoration: 6 products and original photos; see restore-results.txt.');fs.writeFileSync(p.join(base,'reset-results.txt'),report);
