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
        try { return Run(args); } catch (Exception ex) { if (args.Length == 0) MessageBox.Show(ex.Message); else Console.Error.WriteLine(ex.Message); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args) {
        if (args.Length > 0) {
            if (args.Length == 2 && args[0] == "--create-vault") IssuerService.Create(args[1]);
            else if (args.Length == 3 && args[0] == "--export-public") IssuerService.ExportPublic(args[1], args[2]);
            else if (args.Length == 3 && args[0] == "--export-records") IssuerService.ExportRecords(args[1], args[2]);
            else if (args.Length == 6 && args[0] == "--issue") File.WriteAllText(args[5], IssuerService.Issue(args[1], LicenseProtocol.ReadFile(args[2]), args[3], args[4] == "forever" ? (DateTime?)null : LicenseProtocol.Time(args[4])), LicenseProtocol.Utf8);
            else return 2;
            return 0;
        }
        AppFont.Init(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AdminForm()); return 0;
    }
}

class AdminForm : Form
{
    string vault = "";
    Label lblKey;
    TextBox txtName, txtMid, txtOut;
    DateTimePicker dtp;
    NumericUpDown numDays;
    bool _sync;
    CheckBox chkForever;


    public AdminForm()
    {
        Text = "Lumivolt - 라이선스 발급 (관리자 전용)";
        Width = 600; Height = 600;
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.FontBase();

        var header = Theme.BuildHeader("라이선스 발급", "관리자 전용   ·   절대 배포 금지");
        lblKey = new Label { Left = 16, Top = 76, Width = 560, Height = 34 };
        var l1 = new Label { Left = 16, Top = 118, AutoSize = true, Text = "사용자 이름 / 메모" };
        txtName = new TextBox { Left = 16, Top = 140, Width = 550 };
        var l2 = new Label { Left = 16, Top = 174, AutoSize = true, Text = "기기 등록 요청 (Lumivolt / automation 권한)" };
        txtMid = new TextBox { Left = 16, Top = 196, Width = 418, Font = Theme.FontMono(11f) };
        var l3 = new Label { Left = 16, Top = 234, AutoSize = true, Text = "사용 기간 (만료일 또는 일수 입력, 만료일 당일 23:59까지 사용)" };
        dtp = new DateTimePicker { Left = 16, Top = 256, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(30) };
        chkForever = new CheckBox { Left = 190, Top = 258, AutoSize = true, Text = "무기한" };
        numDays = new NumericUpDown { Left = 300, Top = 254, Width = 70, Minimum = 1, Maximum = 3650, Value = 30 };
        var lblDays = new Label { Left = 374, Top = 258, AutoSize = true, Text = "일 (오늘부터)" };
        var b7 = new Button { Left = 16, Top = 284, Width = 60, Height = 28, Text = "7일" };
        var b30 = new Button { Left = 80, Top = 284, Width = 60, Height = 28, Text = "30일" };
        var b90 = new Button { Left = 144, Top = 284, Width = 60, Height = 28, Text = "90일" };
        var b365 = new Button { Left = 208, Top = 284, Width = 60, Height = 28, Text = "1년" };
        var bGen = new Button { Left = 16, Top = 320, Width = 550, Height = 36, Text = "라이선스 키 발급" };
        var l4 = new Label { Left = 16, Top = 366, AutoSize = true, Text = "발급된 키 (사용자에게 그대로 전달)" };
        txtOut = new TextBox { Left = 16, Top = 388, Width = 550, Height = 120, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = Theme.FontMono(9.5f) };
        var bCopy = new Button { Left = 16, Top = 518, Width = 130, Height = 30, Text = "키 복사" };
        var bRequest = new Button { Left = 442, Top = 194, Width = 124, Height = 28, Text = "요청 열기" };
        var bSave = new Button { Left = 154, Top = 518, Width = 120, Height = 30, Text = "파일 저장" };
        var bHistory = new Button { Left = 282, Top = 518, Width = 120, Height = 30, Text = "발급 기록" };
        var bKeys = new Button { Left = 410, Top = 518, Width = 156, Height = 30, Text = "발급키 관리" };
        Controls.AddRange(new Control[] { bRequest, bSave, bHistory, bKeys, lblKey, l1, txtName, l2, txtMid, l3, dtp, chkForever, numDays, lblDays, b7, b30, b90, b365, bGen, l4, txtOut, bCopy, header });

        Safe(delegate { if (Directory.Exists(IssuerService.LastVault)) LoadVault(IssuerService.LastVault); else lblKey.Text = "발급키 관리에서 관리자 보관 폴더를 선택해 주세요."; });

        bRequest.Click += delegate { Safe(delegate { using (var d = new OpenFileDialog { Filter = "등록 요청|*.lvrequest" }) if (d.ShowDialog(this) == DialogResult.OK) { string t = LicenseProtocol.ReadFile(d.FileName); LicenseProtocol.VerifyRequest(t, DateTime.UtcNow); txtMid.Text = t; } }); };
        bSave.Click += delegate { Safe(delegate { if (!txtOut.Text.StartsWith("LV3.")) throw new InvalidOperationException("먼저 발급해 주세요."); using (var d = new SaveFileDialog { Filter = "라이선스|*.lvlicense", FileName = "Lumivolt.lvlicense" }) if (d.ShowDialog(this) == DialogResult.OK) File.WriteAllText(d.FileName, txtOut.Text, LicenseProtocol.Utf8); }); };
        var history = new ContextMenuStrip();
        history.Items.Add("기존 라이선스 조회", null, delegate { Safe(delegate { RequireVault(); using (var d = new OpenFileDialog { Filter = "라이선스|*.lvlicense", InitialDirectory = Path.Combine(vault, "records") }) if (d.ShowDialog(this) == DialogResult.OK) txtOut.Text = IssuerService.Inspect(vault, LicenseProtocol.ReadFile(d.FileName)).ToString(); }); });
        history.Items.Add("발급 기록 JSON 내보내기", null, delegate { Safe(delegate { RequireVault(); using (var d = new SaveFileDialog { Filter = "JSON|*.json", FileName = "issued-licenses.json" }) if (d.ShowDialog(this) == DialogResult.OK) IssuerService.ExportRecords(vault, d.FileName); }); });
        bHistory.Click += delegate { history.Show(bHistory, new Point(0, bHistory.Height)); };
        var keys = new ContextMenuStrip();
        keys.Items.Add("기존 발급키 보관 폴더 열기", null, delegate { ChooseVault(false); });
        keys.Items.Add("새 발급키 생성 (고객 앱 재빌드 필요)", null, delegate { ChooseVault(true); });
        keys.Items.Add("공개키 내보내기", null, delegate { Safe(delegate { RequireVault(); using (var d = new SaveFileDialog { Filter = "공개키 JSON|*.json", FileName = "issuer-public.json" }) if (d.ShowDialog(this) == DialogResult.OK) IssuerService.ExportPublic(vault, d.FileName); }); });
        keys.Items.Add("암호화 백업 만들기", null, delegate { Safe(delegate { RequireVault(); using (var d = new SaveFileDialog { Filter = "암호화 키 백업|*.lvbackup", FileName = "Lumivolt-issuer.lvbackup" }) if (d.ShowDialog(this) == DialogResult.OK) { string password = Password(true); if (password != null) { IssuerService.Backup(vault, d.FileName, password); MessageBox.Show(this, "백업을 만들었습니다. 백업과 암호는 별도로 보관하세요."); } } }); });
        keys.Items.Add("암호화 백업에서 복원", null, delegate { Safe(delegate { using (var d = new OpenFileDialog { Filter = "암호화 키 백업|*.lvbackup" }) if (d.ShowDialog(this) == DialogResult.OK) { using (var folder = new FolderBrowserDialog { Description = "복원할 비어 있는 새 관리자 폴더" }) if (folder.ShowDialog(this) == DialogResult.OK) { string password = Password(false); if (password != null) { IssuerService.Restore(d.FileName, folder.SelectedPath, password); LoadVault(folder.SelectedPath); } } } }); });
        bKeys.Click += delegate { keys.Show(bKeys, new Point(0, bKeys.Height)); };
        Theme.MarkPrimary(bGen);
        Theme.Apply(this);
        chkForever.CheckedChanged += delegate { dtp.Enabled = !chkForever.Checked; numDays.Enabled = !chkForever.Checked; };
        numDays.ValueChanged += delegate { if (_sync) return; _sync = true; dtp.Value = DateTime.Today.AddDays((double)numDays.Value); _sync = false; };
        dtp.ValueChanged += delegate
        {
            if (_sync) return; _sync = true;
            int d = (int)(dtp.Value.Date - DateTime.Today).TotalDays;
            numDays.Value = Math.Max(numDays.Minimum, Math.Min(numDays.Maximum, d));
            _sync = false;
        };
        b7.Click += delegate { numDays.Value = 7; chkForever.Checked = false; };
        b30.Click += delegate { numDays.Value = 30; chkForever.Checked = false; };
        b90.Click += delegate { numDays.Value = 90; chkForever.Checked = false; };
        b365.Click += delegate { numDays.Value = 365; chkForever.Checked = false; };
        bCopy.Click += delegate { if (txtOut.Text.Length > 0) Clipboard.SetText(txtOut.Text); };
        bGen.Click += delegate
        {
            Safe(delegate {
                RequireVault();
                txtOut.Text = IssuerService.Issue(vault, txtMid.Text, txtName.Text, chkForever.Checked ? (DateTime?)null : dtp.Value.Date.AddDays(1));
            });
        };
    }

    void RequireVault() { if (vault.Length == 0) throw new InvalidOperationException("먼저 발급키 보관 폴더를 선택해 주세요."); }
    string Password(bool confirm)
    {
        using (var f = new Form { Text = "발급키 백업 암호", Width = 420, Height = confirm ? 220 : 160, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Theme.FontBase() })
        {
            var label = new Label { Left = 16, Top = 12, Width = 380, Text = "12자 이상의 암호" };
            var first = new TextBox { Left = 16, Top = 38, Width = 370, UseSystemPasswordChar = true };
            var second = new TextBox { Left = 16, Top = 78, Width = 370, UseSystemPasswordChar = true, Visible = confirm };
            var ok = new Button { Text = "확인", Left = 286, Top = confirm ? 125 : 76, Width = 100 };
            ok.Click += delegate { if (first.Text.Length < 12 || (confirm && first.Text != second.Text)) { MessageBox.Show(f, "12자 이상이며 두 입력이 일치해야 합니다."); return; } f.DialogResult = DialogResult.OK; };
            f.Controls.AddRange(new Control[] { label, first, second, ok }); f.AcceptButton = ok; Theme.Apply(f);
            return f.ShowDialog(this) == DialogResult.OK ? first.Text : null;
        }
    }
    void Safe(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, "작업 실패: " + ex.Message); } }
    void LoadVault(string folder) { var pub = IssuerService.Public(folder); vault = folder; IssuerService.LastVault = folder; lblKey.Text = "Ed25519: " + (string)pub["keyId"] + "\r\n관리자 개인키: DPAPI + 계정 접근 권한 보호 · 고객 배포 제외"; }
    void ChooseVault(bool create) { Safe(delegate { using (var d = new FolderBrowserDialog { Description = "관리자 전용 보관 폴더 (새 키 생성 시 비어 있는 새 폴더)" }) if (d.ShowDialog(this) == DialogResult.OK) { if (create) IssuerService.Create(d.SelectedPath); LoadVault(d.SelectedPath); if (create) MessageBox.Show(this, "새 공개키를 내보내고 Build.ps1 -PublicKeyFile로 고객 앱을 다시 빌드해야 합니다."); } }); }
}
