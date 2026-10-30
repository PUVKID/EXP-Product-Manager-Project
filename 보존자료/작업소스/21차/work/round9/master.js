const fs=require('fs'),p=require('path'),r=p.resolve('work/round9/Lumivolt_v2.2.0/3_소스코드');
function edit(f,fn){let q=p.join(r,f);fs.writeFileSync(q,fn(fs.readFileSync(q,'utf8').replace(/\r\n/g,'\n')).replace(/^\uFEFF/,''));}
edit('admin/LicenseAdmin.cs',s=>{
 const start=s.indexOf('class AdminForm : Form'), tail=s.indexOf('    string Password(bool confirm)',start);
 const helpers=s.slice(tail).replace('var pub = IssuerService.Public(folder); vault = folder;','if (!AdminWorkflow.IsMatchingVault(folder)) throw new InvalidOperationException("이 EXP 버전과 맞지 않는 발급키입니다. 기존 키를 선택해 주세요."); vault = folder;').replace(/lblKey.Text = "Ed25519:[^;]+;/,'lblKey.Text = "발급 준비 완료  ·  기존 보안키 연결됨";');
 let program=s.slice(0,start).replace('IssuerService.Issue(args[1], LicenseProtocol.ReadFile(args[2]), args[3], args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4]))','AdminWorkflow.Issue(args[1], LicenseProtocol.ReadFile(args[2]), args[3], args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4]), "Pro")').replace('            else return 2;',`            else if (args.Length == 7 && args[0] == "--issue-edition") File.WriteAllText(args[6], AdminWorkflow.Issue(args[1],LicenseProtocol.ReadFile(args[2]),args[3],args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4]),args[5]),LicenseProtocol.Utf8);
            else if (args.Length == 5 && args[0] == "--trial") File.WriteAllText(args[4], TrialIssuer.Issue(args[1],LicenseProtocol.ReadFile(args[2]),args[3]),LicenseProtocol.Utf8);
            else if (args.Length == 5 && args[0] == "--credits") File.WriteAllText(args[4],CreditIssuer.Issue(args[1],LicenseProtocol.ReadFile(args[2]),int.Parse(args[3])),LicenseProtocol.Utf8);
            else return 2;`);
 return program+`class AdminForm : Form
{
    string vault = ""; Label lblKey, status, summary;
    TextBox txtName, txtMid, txtOut; DateTimePicker dtp; NumericUpDown numDays, numCredits;
    CheckBox chkForever; ComboBox edition, mode; bool syncing;
    public AdminForm()
    {
        Theme.Set(Design.AdminDark ? ThemePalettes.DarkPal : ThemePalettes.Light);
        Text = Brand.Master; ClientSize = new Size(980,760); Font = Theme.FontBase(); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        Controls.Add(new BrandMark { Left=32,Top=30,Width=46,Height=46 });
        Controls.Add(Design.Label(Brand.Master,94,28,430,38,24,"t"));
        Controls.Add(Design.Label("윤석님의 라이선스 워크스페이스",96,71,420,24,10,"m"));
        var light=Design.Button("라이트",754,36,90,false); var dark=Design.Button("다크",852,36,90,false);
        light.Click+=delegate { SetTheme(false); }; dark.Click+=delegate { SetTheme(true); };
        Controls.AddRange(new Control[] { light,dark });
        lblKey=Design.Label("기존 발급키를 찾는 중…",34,116,840,25,10,"m"); Controls.Add(lblKey);
        var input=new SoftCard { Left=32,Top=160,Width=458,Height=516 };
        input.Controls.Add(Design.Label("발급 설정",24,20,400,28,16,"t"));
        input.Controls.Add(Design.Label("발급 종류",24,64,400,22,10,"m"));
        mode=Combo(24,92,410,new[]{"정식 라이선스","Pro 체험판 · 발급 후 3시간","Pro 추가 등록권"});
        input.Controls.Add(mode);
        input.Controls.Add(Design.Label("고객 메모 · 체험판은 신청 이메일 필수",24,132,410,23,10,"m"));
        txtName=Design.Input("customer",24,162,410,34,false); input.Controls.Add(txtName);
        input.Controls.Add(Design.Label("고객 등록코드",24,210,250,23,10,"m"));
        var paste=Design.Button("붙여넣기",310,202,124,false);
        txtMid=Design.Input("request",24,246,410,70,false); input.Controls.AddRange(new Control[]{paste,txtMid});
        paste.Click+=delegate { Safe(delegate { txtMid.Text=LicenseText.Normalize(Clipboard.GetText(),"LVR3"); status.Text="등록코드를 확인했습니다."; }); };
        input.Controls.Add(Design.Label("제품 등급",24,330,180,22,10,"m"));
        edition=Combo(24,360,192,new[]{"Classic · 하루 25개","Studio · 50개 + 반복","Pro · 100개 + 반복"});
        input.Controls.Add(edition);
        numDays=new NumericUpDown { Left=236,Top=360,Width=80,Minimum=1,Maximum=3650,Value=30 };
        chkForever=new CheckBox { Left=328,Top=359,Width=104,Text="무기한" };
        input.Controls.Add(Design.Label("사용 기간 (일)",236,330,198,22,10,"m"));
        input.Controls.AddRange(new Control[]{numDays,chkForever});
        dtp=new DateTimePicker { Left=24,Top=410,Width=192,Format=DateTimePickerFormat.Short,Value=DateTime.Today.AddDays(29) };
        var dateHint=Design.Label("선택한 만료일 23:59까지",236,414,200,24,9,"m");
        numCredits=new NumericUpDown { Left=24,Top=410,Width=192,Minimum=1,Maximum=100000,Value=100,Visible=false };
        input.Controls.AddRange(new Control[]{dtp,dateHint,numCredits});
        summary=Design.Label("",24,462,410,40,10,"m"); input.Controls.Add(summary);
        var output=new SoftCard { Left=506,Top=160,Width=442,Height=516 };
        output.Controls.Add(Design.Label("고객에게 보낼 키",24,20,394,30,16,"t"));
        output.Controls.Add(Design.Label("발급하면 복사되고, 이력도 자동으로 저장됩니다.",24,62,394,46,10,"m"));
        txtOut=Design.Input("issuedKey",24,122,394,194,true); output.Controls.Add(txtOut);
        var issue=Design.Button("발급하고 복사",24,338,394,true); issue.Height=48; output.Controls.Add(issue);
        var copy=Design.Button("다시 복사",24,402,190,false); var save=Design.Button("파일로 저장",224,402,194,false); output.Controls.AddRange(new Control[]{copy,save});
        output.Controls.Add(Design.Label("고객은 받은 키를 붙여 넣기만 하면 됩니다.",24,466,394,30,10,"m"));
        status=Design.Label("등록코드 → 제품·기간 선택 → 발급하고 복사",34,694,800,44,10,"m");
        var manage=Design.Button("관리",850,696,98,false);
        Controls.AddRange(new Control[]{input,output,status,manage});
        Action sync=delegate {
            bool normal=mode.SelectedIndex==0, credits=mode.SelectedIndex==2;
            edition.Enabled=normal; if(!normal) edition.SelectedIndex=2;
            dtp.Visible=!credits; numCredits.Visible=credits; dtp.Enabled=normal&&!chkForever.Checked;
            numDays.Enabled=normal&&!chkForever.Checked; chkForever.Enabled=normal;
            dateHint.Text=credits?"추가 등록 가능 횟수":mode.SelectedIndex==1?"발급 시점부터 정확히 3시간":"선택한 만료일 23:59까지";
            summary.Text=credits?"미사용 잔액 유지 · 같은 키 중복 충전 불가":mode.SelectedIndex==1?"Pro 기능 체험 · 이메일 / PC 중복 발급 차단":"일일 한도는 자정 초기화 · 기본 잔여량 이월 없음";
        };
        mode.SelectedIndexChanged+=delegate { sync(); }; chkForever.CheckedChanged+=delegate { sync(); };
        numDays.ValueChanged+=delegate { if(syncing)return; syncing=true; dtp.Value=DateTime.Today.AddDays((double)numDays.Value-1); syncing=false; };
        dtp.ValueChanged+=delegate { if(syncing)return; syncing=true; numDays.Value=Math.Max(1,Math.Min(3650,(int)(dtp.Value.Date-DateTime.Today).TotalDays+1)); syncing=false; };
        issue.Click+=delegate { Safe(delegate {
            RequireVault();
            if(mode.SelectedIndex==1) txtOut.Text=TrialIssuer.Issue(vault,txtMid.Text,txtName.Text);
            else if(mode.SelectedIndex==2) txtOut.Text=CreditIssuer.Issue(vault,txtMid.Text,(int)numCredits.Value);
            else txtOut.Text=AdminWorkflow.Issue(vault,txtMid.Text,txtName.Text,chkForever.Checked?(DateTime?)null:dtp.Value.Date.AddDays(1),new[]{"Classic","Studio","Pro"}[edition.SelectedIndex]);
            try { Clipboard.SetText(txtOut.Text); status.Text="발급 완료 · 복사한 키를 고객에게 보내 주세요."; } catch { status.Text="발급 완료 · 키를 선택해 직접 복사해 주세요."; }
        }); };
        copy.Click+=delegate { Safe(delegate { if(txtOut.Text.Length>0)Clipboard.SetText(txtOut.Text); }); };
        save.Click+=delegate { Safe(delegate { if(txtOut.Text.Length==0)throw new InvalidOperationException("먼저 키를 발급해 주세요."); using(var d=new SaveFileDialog { Filter="EXP 키|*.expkey",FileName="EXP.expkey" }) if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,txtOut.Text,LicenseProtocol.Utf8); }); };
        var menu=new ContextMenuStrip();
        menu.Items.Add("기존 보안키 연결",null,delegate { ChooseVault(false); });
        menu.Items.Add("등록코드 파일 열기",null,delegate { Safe(delegate { using(var d=new OpenFileDialog { Filter="등록코드|*.lvrequest" })if(d.ShowDialog(this)==DialogResult.OK)txtMid.Text=LicenseText.Normalize(LicenseProtocol.ReadFile(d.FileName),"LVR3"); }); });
        menu.Items.Add("발급 이력 폴더",null,delegate { Safe(delegate { RequireVault(); var dir=Path.Combine(vault,"records"); Directory.CreateDirectory(dir); System.Diagnostics.Process.Start("explorer.exe",dir); }); });
        menu.Items.Add("발급 이력 내보내기",null,delegate { Safe(delegate { RequireVault(); using(var d=new SaveFileDialog { Filter="JSON|*.json",FileName="EXP-records.json" })if(d.ShowDialog(this)==DialogResult.OK)IssuerService.ExportRecords(vault,d.FileName); }); });
        menu.Items.Add("보안키 암호화 백업",null,delegate { Safe(delegate { RequireVault(); using(var d=new SaveFileDialog { Filter="암호화 백업|*.lvbackup",FileName="EXP-Master.lvbackup" })if(d.ShowDialog(this)==DialogResult.OK){string password=Password(true);if(password!=null)IssuerService.Backup(vault,d.FileName,password);} }); });
        menu.Items.Add("보안키 백업 복원",null,delegate { Safe(delegate { using(var d=new OpenFileDialog {Filter="암호화 백업|*.lvbackup"})if(d.ShowDialog(this)==DialogResult.OK)using(var dir=new FolderBrowserDialog {Description="비어 있는 새 관리자 폴더"})if(dir.ShowDialog(this)==DialogResult.OK){string password=Password(false);if(password!=null){IssuerService.Restore(d.FileName,dir.SelectedPath,password);LoadVault(dir.SelectedPath);}} }); });
        manage.Click+=delegate { menu.Show(manage,new Point(0,manage.Height)); };
        string found=AdminWorkflow.FindVault(); if(found.Length>0)LoadVault(found); else lblKey.Text="기존 보안키를 연결해 주세요. 관리 → 기존 보안키 연결";
        sync(); Design.Scale(this); Theme.Apply(this);
    }
    ComboBox Combo(int x,int y,int width,string[] values) {var c=new ComboBox{Left=x,Top=y,Width=width,DropDownStyle=ComboBoxStyle.DropDownList};c.Items.AddRange(values);c.SelectedIndex=0;return c;}
    void SetTheme(bool dark) { Design.AdminDark=dark;Theme.Set(dark?ThemePalettes.DarkPal:ThemePalettes.Light);Theme.Apply(this);Dwm.Apply(this,Theme.Current);Invalidate(true); }
    void RequireVault() { if(!AdminWorkflow.IsMatchingVault(vault))throw new InvalidOperationException("관리 메뉴에서 기존 보안키를 연결해 주세요."); }
`+helpers;
});
edit('src/UI/AutomationController.cs',s=>s.replace(/DateTime.Now/g,'ServerClock.KoreaNow').replace(/DateTime.Today/g,'ServerClock.KoreaNow.Date').replace('try { UsageQuota.RequireMode','try { ServerClock.RequireFresh(); UsageQuota.RequireMode').replace('        public void Tick()\n        {','        public void Tick()\n        {\n            ServerClock.RefreshAsync();\n            if (!ServerClock.Fresh) { if (Active) { NextText = "서버 시각 확인 중 · 예약 대기"; Raise(); } return; }'));
edit('src/MainForm.cs',s=>s.replace('host.Form = this;','ServerClock.RefreshAsync();\n            host.Form = this;'));
edit('src/Security/UsageQuota.cs',s=>s.replace(/DateTime.UtcNow/g,'ServerClock.UtcNow').replace('public static void Consume(string shopId) {','public static void Consume(string shopId) {\n            ServerClock.RequireFresh();'));
edit('src/License.cs',s=>s.replace('TrustedKeys.All, DateTime.UtcNow','TrustedKeys.All, ServerClock.UtcNow'));
edit('src/UI/Pages/LicensePage.cs',s=>s.replace('lblName.Text = li.Name ?? "-";','lblName.Text = "EXP " + li.Edition + (li.Trial ? " · 체험판" : "") + " / " + li.Name;').replace('li.Expires.ToString("yyyy-MM-dd")','li.Expires.ToString("yyyy-MM-dd HH:mm")').replace('lblLeft.Text = days > 0 ? days + "일 남음" : "만료됨";','var left = li.Expires.ToUniversalTime() - ServerClock.UtcNow;\n                lblLeft.Text = left.TotalHours < 24 ? Math.Max(0,(int)left.TotalHours) + "시간 " + Math.Max(0,left.Minutes) + "분 남음" : days + "일 남음";'));
edit('build/Build.ps1',s=>s.replace(/'admin\\CreditIssuer.cs'/g,"'admin\\CreditIssuer.cs','admin\\TrialIssuer.cs'"));
