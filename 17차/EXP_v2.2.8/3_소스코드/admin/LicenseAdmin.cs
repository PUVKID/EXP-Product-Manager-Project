using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using BunjangAuto;
using Lumivolt.Security;
using Lumivolt.Admin;
using System.Reflection;

static class AdminProgram
{
    [STAThread]
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) {
                if (stream == null) return null;
                using (var m = new MemoryStream()) { stream.CopyTo(m); return Assembly.Load(m.ToArray()); }
            }
        };
        try { return Run(args); } catch (Exception ex) { if (args.Length == 0) AppDialog.Show(ex.Message); else Console.Error.WriteLine(ex.Message); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args) {
        if (args.Length > 0) {
            if (args.Length == 2 && args[0] == "--create-vault") IssuerService.Create(args[1]);
            else if (args.Length == 3 && args[0] == "--export-public") IssuerService.ExportPublic(args[1], args[2]);
            else if (args.Length == 3 && args[0] == "--export-records") IssuerService.ExportRecords(args[1], args[2]);
            else if (args.Length == 6 && args[0] == "--issue") File.WriteAllText(args[5], AdminWorkflow.Issue(args[1], LicenseProtocol.ReadFile(args[2]), args[3], args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4]), "Pro"), LicenseProtocol.Utf8);
            else if (args.Length == 7 && args[0] == "--issue-edition") File.WriteAllText(args[6], AdminWorkflow.Issue(args[1],LicenseProtocol.ReadFile(args[2]),args[3],args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4]),args[5]),LicenseProtocol.Utf8);
            else if (args.Length == 5 && args[0] == "--trial") File.WriteAllText(args[4], TrialIssuer.Issue(args[1],LicenseProtocol.ReadFile(args[2]),args[3]),LicenseProtocol.Utf8);
            else if (args.Length == 5 && args[0] == "--credits") File.WriteAllText(args[4],CreditIssuer.Issue(args[1],LicenseProtocol.ReadFile(args[2]),int.Parse(args[3])),LicenseProtocol.Utf8);
            else return 2;
            return 0;
        }
        AppFont.Init(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AdminForm()); return 0;
    }
}

class AdminForm : Form
{
    string vault = ""; Label lblKey, status, summary;
    SoftInput txtName, txtMid, txtOut; SoftDatePicker dtp; SoftNumber numDays, numCredits;
    CheckBox chkForever; ComboBox edition, mode; bool syncing;
    public AdminForm()
    {
        Theme.Set(Design.AdminDark ? ThemePalette.DarkPal : ThemePalette.Light);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.app.ico")) if(stream!=null) Icon=new Icon(stream);
        Text = Brand.Master; ClientSize = new Size(980,808); Font = Theme.FontBase(); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        Controls.Add(new HeroBanner { Left=32,Top=24,Width=916,Height=128,Eyebrow="EXP MASTER  /  윤석",Title="키 발급, 더 간결하게.",Subtitle="등록코드 하나로 제품과 기간을 연결하세요." });
        var light=Design.Button("라이트",754,164,90,false); var dark=Design.Button("다크",852,164,90,false);
        light.Click+=delegate { SetTheme(false); }; dark.Click+=delegate { SetTheme(true); };
        Controls.AddRange(new Control[] { light,dark });
        lblKey=Design.Label("기존 발급키를 찾는 중…",34,170,690,25,10,"m"); Controls.Add(lblKey);
        var input=new SoftCard { Left=32,Top=216,Width=458,Height=516 };
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
        numDays=new SoftNumber { Left=236,Top=360,Width=144,Minimum=1,Maximum=3650,Value=30 };
        chkForever=new CheckBox { Left=236,Top=404,Width=104,Text="무기한" };
        input.Controls.Add(Design.Label("사용 기간 (일)",236,330,198,22,10,"m"));
        input.Controls.AddRange(new Control[]{numDays,chkForever});
        dtp=new SoftDatePicker { Left=24,Top=410,Width=192,Format=DateTimePickerFormat.Short,Value=DateTime.Today.AddDays(29) };
        var dateHint=Design.Label("선택한 만료일 23:59까지",236,434,200,24,9,"m");
        numCredits=new SoftNumber { Left=24,Top=410,Width=192,Minimum=1,Maximum=100000,Value=100,Visible=false };
        input.Controls.AddRange(new Control[]{dtp,dateHint,numCredits});
        summary=Design.Label("",24,462,410,40,10,"m"); input.Controls.Add(summary);
        var output=new SoftCard { Left=506,Top=216,Width=442,Height=516 };
        output.Controls.Add(Design.Label("고객에게 보낼 키",24,20,394,30,16,"t"));
        output.Controls.Add(Design.Label("발급하면 복사되고, 이력도 자동으로 저장됩니다.",24,62,394,46,10,"m"));
        txtOut=Design.Input("issuedKey",24,122,394,194,true); output.Controls.Add(txtOut);
        var issue=Design.Button("발급하고 복사",24,338,394,true); issue.Height=48; output.Controls.Add(issue);
        var copy=Design.Button("다시 복사",24,402,190,false); var save=Design.Button("파일로 저장",224,402,194,false); output.Controls.AddRange(new Control[]{copy,save});
        output.Controls.Add(Design.Label("고객은 받은 키를 붙여 넣기만 하면 됩니다.",24,466,394,30,10,"m"));
        status=Design.Label("등록코드 → 제품·기간 선택 → 발급하고 복사",34,748,800,44,10,"m");
        var manage=Design.Button("관리",850,748,98,false);
        Controls.AddRange(new Control[]{input,output,status,manage});
        Action sync=delegate {
            bool normal=mode.SelectedIndex==0, credits=mode.SelectedIndex==2;
            edition.Enabled=normal; if(!normal) edition.SelectedIndex=2;
            dtp.Visible=normal; numCredits.Visible=credits; dtp.Enabled=normal&&!chkForever.Checked;
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
    ComboBox Combo(int x,int y,int width,string[] values) {var c=new SoftCombo{Left=x,Top=y,Width=width};c.Items.AddRange(values);c.SelectedIndex=0;return c;}
    void SetTheme(bool dark) { Design.AdminDark=dark;Theme.Set(dark?ThemePalette.DarkPal:ThemePalette.Light);Theme.Apply(this);Dwm.Apply(this,Theme.Current);Invalidate(true); }
    void RequireVault() { if(!AdminWorkflow.IsMatchingVault(vault))throw new InvalidOperationException("관리 메뉴에서 기존 보안키를 연결해 주세요."); }
    string Password(bool confirm)
    {
        using (var f = new Form { Text = "발급키 백업 암호", Width = 420, Height = confirm ? 220 : 160, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Theme.FontBase(), Icon = this.Icon })
        {
            var label = new Label { Left = 16, Top = 12, Width = 380, Text = "12자 이상의 암호" };
            var first = new SoftInput { Left = 16, Top = 38, Width = 370, UseSystemPasswordChar = true };
            var second = new SoftInput { Left = 16, Top = 78, Width = 370, UseSystemPasswordChar = true, Visible = confirm };
            var ok = new Button { Text = "확인", Left = 286, Top = confirm ? 125 : 76, Width = 100 };
            ok.Click += delegate { if (first.Text.Length < 12 || (confirm && first.Text != second.Text)) { AppDialog.Show(f, "12자 이상이며 두 입력이 일치해야 합니다."); return; } f.DialogResult = DialogResult.OK; };
            f.Controls.AddRange(new Control[] { label, first, second, ok }); f.AcceptButton = ok; Theme.Apply(f);
            return f.ShowDialog(this) == DialogResult.OK ? first.Text : null;
        }
    }
    void Safe(Action action) { try { action(); } catch (Exception ex) { AppDialog.Show(this, "작업 실패: " + ex.Message); } }
    void LoadVault(string folder) { if (!AdminWorkflow.IsMatchingVault(folder)) throw new InvalidOperationException("이 EXP 버전과 맞지 않는 발급키입니다. 기존 키를 선택해 주세요."); vault = folder; IssuerService.LastVault = folder; lblKey.Text = "발급 준비 완료  ·  기존 보안키 연결됨"; }
    void ChooseVault(bool create) { Safe(delegate { using (var d = new FolderBrowserDialog { Description = "관리자 전용 보관 폴더 (새 키 생성 시 비어 있는 새 폴더)" }) if (d.ShowDialog(this) == DialogResult.OK) { if (create) IssuerService.Create(d.SelectedPath); LoadVault(d.SelectedPath); if (create) AppDialog.Show(this, "새 공개키를 내보내고 Build.ps1 -PublicKeyFile로 고객 앱을 다시 빌드해야 합니다."); } }); }
}

