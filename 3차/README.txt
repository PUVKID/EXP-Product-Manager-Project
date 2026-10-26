[번개장터 자동등록 - 전체 파일]  Copyright (c) 음악천재. All rights reserved.

1_배포용_앱/BunjangAuto.exe
   사용자에게 배포하는 파일. 처음 실행 시 이용약관 동의 -> PC 고유번호 표시 -> 관리자가 발급한 키 입력 순서로 진행된다.
   키는 PC 고유번호에 묶여 있어 다른 PC에서는 동작하지 않는다.
   디버거/리버싱 도구(dnSpy, ILSpy, x64dbg, Cheat Engine, ProcessHacker 등)가 감지되면 즉시 종료되며,
   실행 중에도 2분마다 재확인한다.

2_관리자용_발급툴/ (절대 배포 금지)
   LicenseAdmin.exe + license_private.xml + issued_licenses.csv (세 파일을 같은 폴더에 둘 것)
   사용자에게서 PC 고유번호(XXXX-XXXX-XXXX)를 받아 이름, 번호, 기간(일수 또는 날짜)을 넣고 발급한다.
   발급 내역은 issued_licenses.csv 에 자동으로 쌓인다.

3_소스코드/
   src, admin, lib, build.bat(Windows), build.sh / build_admin.sh(Mono), kg.cs(키 생성기)
   license_private.xml = 개인키(절대 공개 금지), license_public.xml = 공개키(앱에 변형되어 내장됨)
   새 키쌍이 필요하면 kg.cs 를 컴파일/실행하고, src/License.cs 안의 _b 값(공개키, XOR로 가려짐)을
   새 공개키로 바꾼 뒤 build.sh/build.bat 로 앱과 관리자툴을 다시 빌드한다. (교체 시 기존 키는 모두 무효)

이용약관(Terms.cs)에 저작권자 "음악천재" 명의, 무단 복제/배포/역분석/우회 금지 및
위반 시 민형사상 책임을 명시했고, 앱 실행 시 동의해야 다음 단계로 진행된다.
