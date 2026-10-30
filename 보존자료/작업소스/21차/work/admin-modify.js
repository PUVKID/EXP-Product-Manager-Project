const fs=require('fs'),path=require('path'),root=path.resolve('work/Lumivolt_v2.1.0/3_소스코드');
const f=path.join(root,'admin/LicenseAdmin.cs');
let s=fs.readFileSync(f,'utf8');
s=s.replace('using BunjangAuto;', 'using BunjangAuto;\nusing Lumivolt.Security;\nusing Lumivolt.Admin;\nusing System.Reflection;');
s=s.replace('    static void Main()\n    {\n        Application.EnableVisualStyles();\n        Application.Run(new AdminForm());\n    }',`    static int Main(string[] args)
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
    }`);
s=s.replace('    string _priv = "";','    string vault = "";');
s=s.replace('    string Dir { get { return AppDomain.CurrentDomain.BaseDirectory; } }\n    string PrivPath { get { return Path.Combine(Dir, "license_private.xml"); } }','');
s=s.replace('사용자 PC 고유번호 (인증 창에 표시된 XXXX-XXXX-XXXX)','기기 등록 요청 (Lumivolt / automation 권한)');
s=s.replace('Left = 16, Top = 196, Width = 550','Left = 16, Top = 196, Width = 418');
s=s.replace('        Controls.AddRange(new Control[] { lblKey,',`        var bRequest = new Button { Left = 442, Top = 194, Width = 124, Height = 28, Text = "요청 열기" };
        var bSave = new Button { Left = 154, Top = 518, Width = 120, Height = 30, Text = "파일 저장" };
        var bHistory = new Button { Left = 282, Top = 518, Width = 120, Height = 30, Text = "발급 기록" };
        var bKeys = new Button { Left = 410, Top = 518, Width = 156, Height = 30, Text = "발급키 관리" };
        Controls.AddRange(new Control[] { bRequest, bSave, bHistory, bKeys, lblKey,`);
s=s.replace('        LoadKey();','        Safe(delegate { if (Directory.Exists(IssuerService.LastVault)) LoadVault(IssuerService.LastVault); else lblKey.Text = "발급키 관리에서 관리자 보관 폴더를 선택해 주세요."; });');
const a=s.indexOf('            if (_priv.Length'),b=s.indexOf('\n        };',a);
s=s.slice(0,a)+`            Safe(delegate {
                RequireVault();
                txtOut.Text = IssuerService.Issue(vault, txtMid.Text, txtName.Text, chkForever.Checked ? (DateTime?)null : dtp.Value.Date.AddDays(1));
            });`+s.slice(b);
const c=s.indexOf('    void LoadKey()'); s=s.slice(0,c)+`    void RequireVault() { if (vault.Length == 0) throw new InvalidOperationException("먼저 발급키 보관 폴더를 선택해 주세요."); }
    void Safe(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, "작업 실패: " + ex.Message); } }
    void LoadVault(string folder) { var pub = IssuerService.Public(folder); vault = folder; IssuerService.LastVault = folder; lblKey.Text = "Ed25519: " + (string)pub["keyId"] + "\\r\\n관리자 개인키: DPAPI + 계정 접근 권한 보호 · 고객 배포 제외"; }
    void ChooseVault(bool create) { Safe(delegate { using (var d = new FolderBrowserDialog { Description = "관리자 전용 보관 폴더 (새 키 생성 시 비어 있는 새 폴더)" }) if (d.ShowDialog(this) == DialogResult.OK) { if (create) IssuerService.Create(d.SelectedPath); LoadVault(d.SelectedPath); if (create) MessageBox.Show(this, "새 공개키를 내보내고 Build.ps1 -PublicKeyFile로 고객 앱을 다시 빌드해야 합니다."); } }); }
}
`;
const injection=`
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
        bKeys.Click += delegate { keys.Show(bKeys, new Point(0, bKeys.Height)); };
`;
s=s.replace('        Theme.MarkPrimary(bGen);',injection+'        Theme.MarkPrimary(bGen);');
fs.writeFileSync(f,'\ufeff'+s,'utf8');
