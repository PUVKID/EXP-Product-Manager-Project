const fs=require('fs'),p=require('path');
const root=p.resolve('work/round11b/source');
fs.cpSync(p.resolve('work/round11/source'),root,{recursive:true,filter:f=>!['dist','bin','obj'].includes(p.basename(f))});
const read=f=>fs.readFileSync(p.join(root,f),'utf8');
const write=(f,s)=>fs.writeFileSync(p.join(root,f),s);
const edit=(f,fn)=>write(f,fn(read(f)));
for(const f of ['src/Brand.cs','src/AssemblyInfo.cs','admin/AssemblyInfo.cs','installer/EXP.iss'])edit(f,s=>s.replaceAll('2.2.1','2.2.2'));
// Remove the transport and its server administration interface from the application.
for(const f of ['TelemetryService','TelemetryConfig','CentralLogService','LogQueue','AdminApiClient','BuildConfig','ClientIdentityService'])fs.unlinkSync(p.join(root,'src/Telemetry/'+f+'.cs'));
for(const f of ['LogPage','AdminPage'])fs.unlinkSync(p.join(root,'src/UI/Pages/'+f+'.cs'));
edit('src/Program.cs',s=>s.replace(/^.*--selftest-central.*\r?\n/m,'').replaceAll('ClientIdentityService.AppVersion','Brand.Version').replaceAll('ClientIdentityService.OsDescription()','Environment.OSVersion.VersionString').replace(/^.*TelemetryService\.(Start|Shutdown).*\r?\n/gm,'').replace('                // 약관에 동의하고 라이선스가 확인된 뒤에만 운영 로그 전송을 시작한다.','                // Recent activity counters are local to this PC.'));
edit('src/Telemetry/CrashReportService.cs',s=>s.replaceAll('ClientIdentityService.AppVersion','Brand.Version').replaceAll('ClientIdentityService.OsDescription()','Environment.OSVersion.VersionString').replace(/^.*TelemetryService.FlushNow.*\r?\n/m,'').replace('가능하면 종료 전에 서버로 마지막 전송을 시도한다.','PC에 보관한다.'));
edit('src/UI/AppHost.cs',s=>s.replace(/        public AdminApiClient Admin;[\s\S]*?        public void RaiseLoginChanged/,`        public event Action LoginChanged;
        public void RaiseLoginChanged`));
edit('src/MainForm.cs',s=>s.replace('chipLic, chipUser','chipLic').replace(/^.*new\[\] \{ "(logs|admin)".*\r?\n/gm,'').replace('if (key == "admin") item.Visible = false; else y += 46;','y += 46;').replace('chipLic = new Chip { Text = "라이선스" }; chipUser = new Chip { Text = "USER" };','chipLic = new Chip { Text = "라이선스" };').replace('lblPage, chipLic, chipUser, segTheme','lblPage, chipLic, segTheme').replace(/^.*factories\["(logs|admin)"\].*\r?\n/gm,'').replace(/^.*host.AdminChanged.*\r?\n/m,'').replaceAll('SyncChips(); SyncRole();','SyncChips();').replace('            if (key == "admin" && !host.IsAdmin) key = "settings";','            if (!factories.ContainsKey(key)) return;').replace(/        private void SyncRole\(\)[\s\S]*?        private void SyncChips/,'        private void SyncChips').replace(/^.*chipUser.Location.*\r?\n/m,'').replace(/^.*host.Admin.Logout.*\r?\n/m,''));
edit('src/UI/Pages/DashboardPage.cs',s=>s.replace('activity, recentLogs','activity').replace('lstActivity, lstLogs','lstActivity').replace('        private readonly FluentButton more;','').replace(/            recentLogs = new CardPanel[\s\S]*?            Controls.AddRange\(new Control\[\] \{ hero, grid, activity, recentLogs \}\);/,'            Controls.AddRange(new Control[] { hero, grid, activity });').replace('Height = 216','Height = 292').replace(/            int gap = Ui.S\(12\);[\s\S]*?        public override void OnActivated/,`            int x0 = Padding.Left, y0 = Padding.Top + Ui.S(474), ch = Ui.S(244);
            activity.SetBounds(x0,y0,w,ch);
            lstActivity.SetBounds(Ui.S(16),Ui.S(52),w-Ui.S(32),ch-Ui.S(68));
        }

        public override void OnActivated`).replace('activity.Invalidate(); recentLogs.Invalidate(); lstActivity.Invalidate(); lstLogs.Invalidate();','activity.Invalidate(); lstActivity.Invalidate();').replace(/            var logs = Log.LoadRecent[\s\S]*?            lstLogs.EndUpdate\(\);/,'').replace('등록 완료(상품번호가 있는 것 포함)','등록을 완료한 상품').replace('아직 올리지 않은 유효한 상품','등록할 준비가 된 상품').replace('전용 크롬에 로그인되어 있음','번개장터 로그인 확인').replace('기간 제한이 없는 라이선스','기간 제한 없음').replace('Host.Products.Count == 0 ? "상품 관리에서 첫 상품을 추가하세요" : "목록에 있는 모든 상품"','"저장한 상품 수"'));
edit('src/UI/Controls.cs',s=>{
const a=s.indexOf('            TextRenderer.DrawText(g, Caption',s.indexOf('public class StatCard'));
const b=s.indexOf('\n        }',a);
return s.slice(0,a)+`            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,Caption,Theme.Font(9.5f,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(14),Width-Ui.S(36),Ui.S(24)),p.TextMuted,flags);
            float size=22f;
            while(size>14f && TextRenderer.MeasureText(g,Value,Theme.Font(size,FontStyle.Bold)).Width>Width-Ui.S(36))size-=1f;
            TextRenderer.DrawText(g,Value,Theme.Font(size,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(44),Width-Ui.S(36),Ui.S(40)),Tone!=null?Tone():p.Text,flags);
            if(Hint.Length>0)TextRenderer.DrawText(g,Hint,Theme.Font(9f,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(100),Width-Ui.S(36),Ui.S(23)),p.TextMuted,flags);`+s.slice(b);
});
edit('src/UI/Pages/HelpPage.cs',s=>s.replace('Height = 78','Height = 62').replace(/^.*var sub = Ui.Muted.*\r?\n/m,'').replace('top.Controls.AddRange(new Control[] { title, sub });','top.Controls.Add(title);'));
edit('src/Terms.cs',s=>s.replace('2026-10-v3','2026-10-v4').replace(/2\. 프로그램은 오류 해결[\s\S]*?제8조/,'2. 상품·설정·사용량과 오류 기록은 이용자의 PC에 보관합니다. 운영 로그와 시스템 정보를 저작권자의 서버로 자동 전송하는 기능은 제공하지 않습니다.\n3. 상품 등록과 서버 시각 확인에는 인터넷 연결이 필요합니다.\n\n제8조'));
edit('src/Bot.cs',s=>s.replace('"계속 안 되면 로그 파일을 보내주세요: " + Path.Combine(Paths.Logs, "chromedriver.log") + "  /  "','"설정에서 자가진단을 실행하거나 "+Brand.Email+"로 문의해 주세요. "'));
edit('src/SelfTestV2.cs',s=>{
let pre=s.slice(0,s.indexOf('        // ---------------------------------------------------------------- Telemetry serialization'));
let red=s.slice(s.indexOf('        private static void RedactionTest()'),s.indexOf('        private static void LogQueueTest()'));
let csv=s.slice(s.indexOf('        private static void CsvCompatTest()'),s.indexOf('        private class FakeTransport'));
if(!csv.includes('private static void CsvCompatTest'))throw Error('Csv method extraction');
return '#if TESTBUILD\n'+(pre+red+csv+'\n    }\n}\n').replace('ThemeTest(); FontTest(); HelpTest(); PauseTest(); StorageMigrationTest(); TelemetrySerializationTest(); RedactionTest(); LogQueueTest(); ClientIdTest(); CsvCompatTest(); TransportTest();','ThemeTest(); FontTest(); HelpTest(); PauseTest(); StorageMigrationTest(); RedactionTest(); CsvCompatTest();')+'#endif\n';
});
edit('src/SelfTest.cs',s=>'#if TESTBUILD\n'+s.replace(/^\ufeff/,'')+'\n#endif\n');
console.log(root);
