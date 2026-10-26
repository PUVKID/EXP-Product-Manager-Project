[번개장터 자동등록 - 전체 파일]  Copyright (c) 음악천재. All rights reserved.

1_배포용_앱/BunjangAuto.exe
   사용자에게 배포하는 파일. 처음 실행 시 이용약관 동의 -> PC 고유번호 표시 -> 관리자가 발급한 키 입력 순서로 진행된다.
   키는 PC 고유번호에 묶여 있어 다른 PC에서는 동작하지 않는다.
   디버거/리버싱 도구(dnSpy, ILSpy, x64dbg, Cheat Engine, ProcessHacker 등)가 감지되면 즉시 종료되며,
   실행 중에도 2분마다 재확인한다.
   화면은 밝은 아이보리 + 골드 톤으로 디자인되어 있다(Theme.cs).

   [보안 강화 내역]
   - 라이선스 서명 키를 RSA-3072 -> RSA-4096 으로 올렸다. license_private.xml / license_public.xml 이
     새로 바뀌었으므로, 과거에 발급했던 라이선스 키가 있다면 전부 무효가 된다. 아래 2_관리자용_발급툴 로 다시 발급해야 한다.
   - 앱에 내장하는 공개키는 예전처럼 단순 XOR가 아니라 AES-256(CBC)으로 암호화해 숨겨 둔다(License.cs).
   - 로컬에 저장하는 설정(settings.json)과 상품 목록(products.json)을 AES-256 + HMAC-SHA256 으로 암호화해서
     저장한다. 키는 비밀번호 없이 "이 PC 고유번호"에서 만들어 내므로, 파일을 다른 PC로 옮기거나 손으로
     열어 봐도 내용을 알 수 없다. 예전 버전이 평문으로 남겨 둔 파일은 처음 실행될 때 자동으로 암호화 형식으로
     바뀐다. 단, PC를 바꾸거나 윈도우를 새로 설치하면 이 두 파일은 복구할 수 없으니, 상품 목록은 평소에도
     '상품 목록' 탭의 [CSV 저장]으로 따로 백업해 두는 것을 권장한다.

2_관리자용_발급툴/ (절대 배포 금지)
   LicenseAdmin.exe + license_private.xml + issued_licenses.csv (세 파일을 같은 폴더에 둘 것)
   사용자에게서 PC 고유번호(XXXX-XXXX-XXXX)를 받아 이름, 번호, 기간(일수 또는 날짜)을 넣고 발급한다.
   발급 내역은 issued_licenses.csv 에 자동으로 쌓인다.
   이번에 license_private.xml 이 RSA-4096 새 키로 교체되었으므로, 1_배포용_앱 의 새 exe와 반드시 짝을 맞춰 써야 한다
   (예전 exe + 새 관리자툴, 또는 새 exe + 예전 관리자툴 조합은 서로 키가 달라 인증이 되지 않는다).

3_소스코드/
   src, admin, lib, build.bat(Windows), build.sh / build_admin.sh(Mono), kg.cs(키 생성기, RSA-4096)
   license_private.xml = 개인키(절대 공개 금지), license_public.xml = 공개키(앱에 AES로 암호화되어 내장됨)
   새 키쌍이 필요하면 kg.cs 를 컴파일/실행하고(기본 4096비트), src/License.cs 안의 공개키 임베딩 값을
   새 공개키로 바꾼 뒤(암호화 과정은 License.cs 상단 주석 참고) build.sh/build.bat 로 앱과 관리자툴을 다시
   빌드한다. (교체 시 기존에 발급된 라이선스 키는 모두 무효)

이용약관(Terms.cs)에 저작권자 "음악천재" 명의, 무단 복제/배포/역분석/우회 금지 및
위반 시 민형사상 책임을 명시했고, 앱 실행 시 동의해야 다음 단계로 진행된다.
