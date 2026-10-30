const fs=require('fs'),p=require('path'),root=p.resolve('work/round11b/source');
function edit(f,fn){const t=p.join(root,f);fs.writeFileSync(t,fn(fs.readFileSync(t,'utf8')));}
edit('src/DiagnosticsForm.cs',s=>s.replace('return info.Valid?Pass(', 'return info.Valid&&LicenseClock.Accept(false)?Pass('));
for(const file of ['PreviewHarness.cs','UiLayoutCheck.cs','UiSelfTest.cs'])fs.unlinkSync(p.join(root,'src/UI',file));
edit('src/Program.cs',s=>s.replace(/#if UIPREVIEW[\s\S]*?#endif\r?\n/,''));
edit('src/UI/Pages/AutomationPage.cs',s=>s.replace('            var sub = Ui.Muted("상품 목록의 상품을 번개장터에 자동으로 올립니다."); sub.Location = new Point(2, 38);','').replace('title, sub, cardMain','title, cardMain'));
edit('src/UI/Pages/LoginPage.cs',s=>s.replace(/^.*var sub = Ui.Muted.*\r?\n/m,'').replace('title, sub, card','title, card').replace(/            var info = Ui.Muted\([\s\S]*?            info.Location/,`            var info = Ui.Muted("1. 로그인 테스트를 누르세요.\\r\\n\\r\\n2. 열린 Chrome에서 번개장터에 로그인하세요.\\r\\n\\r\\n3. 로그인을 마친 뒤 안내창에서 예를 누르세요.");
            info.Location`));
edit('src/SelfTestV2.cs',s=>s.replace('// ---------------------------------------------------------------- Log queue','// ---------------------------------------------------------------- Data compatibility'));
edit('tests/ProductionSmoke.cs',s=>s.replace('bool clean =','bool removed = asm.GetType("BunjangAuto.TelemetryService")==null && asm.GetType("BunjangAuto.CentralLogService")==null && asm.GetType("BunjangAuto.AdminApiClient")==null && asm.GetType("BunjangAuto.LogPage")==null && asm.GetType("BunjangAuto.SelfTest")==null;\n        Console.WriteLine((removed?"PASS ":"FAIL ")+"no telemetry transport, remote administration, log screen or developer self-tests in release");\n        bool clean =').replace('return good.Valid && !bad.Valid && clean','return good.Valid && !bad.Valid && clean && removed'));
edit('tests/RevisionUi.cs',s=>s.replace('string target=Path.GetFullPath(args[0]);','if(args.Length>1){Dpi.ForcedScale=double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture);Dpi.EmulateFontScale=true;}\n        string target=Path.GetFullPath(args[0]);'));
fs.writeFileSync(p.join(root,'tests/WINDOWS_CHECKLIST.md'),`# EXP 2.2.2 화면·동작 확인

- 상품 편집: 라이트/다크, 저장/취소, 긴 상품명, 설명 줄바꿈, 사진 추가/삭제/순서.
- 한눈에 보기: 큰 한글 값과 설명 간격, 최근 활동의 전체 폭, 로그 메뉴 없음.
- 자가진단: 6개 항목, 실제 PASS/FAIL/미검사, 다시 진단, 진단 중 닫기.
- 설정: 테마와 Chrome, 자가진단만 표시. 로그 전송 및 서버 관리자 로그인 없음.
- 사용 설명서: 새 12개 목차, 검색, 폭에 따른 줄바꿈.
- 인증: 기존 키 호환, 다른 기기용 키 거부, 변조 거부, 체험 시간과 일일 한도 유지.
- 배포 EXE: 원격 로그 전송 코드와 개발용 테스트 제외 확인.

실제 상품 등록과 다른 물리 PC는 별도 실환경 확인 대상입니다.
`);
