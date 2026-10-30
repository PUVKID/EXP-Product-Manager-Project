const fs=require('fs'),path=require('path'),r=path.resolve('work/round9/Lumivolt_v2.2.0/3_소스코드');
function edit(p,f){let x=path.join(r,p);fs.writeFileSync(x,'\ufeff'+f(fs.readFileSync(x,'utf8').replace(/^\ufeff/,'')));}
edit('src/Theme.cs',s=>{
 const start=s.indexOf('        public static readonly ThemePalette Light'),end=s.indexOf('\n    /// <summary>Windows 11 창',start);
 const palettes=`        public static readonly ThemePalette Light = new ThemePalette
        {
            Dark = false, Bg = Color.FromArgb(246,247,249), Sidebar = Color.FromArgb(237,239,243),
            Card = Color.White, CardAlt = Color.FromArgb(248,249,251), Field = Color.FromArgb(249,250,252),
            FieldBorder = Color.FromArgb(210,214,222), Border = Color.FromArgb(224,227,233),
            Hover = Color.FromArgb(228,233,241), Selected = Color.FromArgb(216,228,247),
            Accent = Color.FromArgb(29,87,204), AccentHover = Color.FromArgb(24,77,184), AccentPressed = Color.FromArgb(18,62,153), OnAccent = Color.White,
            Text = Color.FromArgb(27,30,37), TextMuted = Color.FromArgb(102,110,125),
            Danger = Color.FromArgb(180,48,58), Success = Color.FromArgb(28,116,82), Warn = Color.FromArgb(142,97,20)
        };
        public static readonly ThemePalette DarkPal = new ThemePalette
        {
            Dark = true, Bg = Color.FromArgb(19,21,26), Sidebar = Color.FromArgb(15,17,21),
            Card = Color.FromArgb(29,32,39), CardAlt = Color.FromArgb(36,40,48), Field = Color.FromArgb(23,26,32),
            FieldBorder = Color.FromArgb(68,76,92), Border = Color.FromArgb(47,53,64),
            Hover = Color.FromArgb(42,49,62), Selected = Color.FromArgb(40,60,94),
            Accent = Color.FromArgb(151,185,255), AccentHover = Color.FromArgb(177,203,255), AccentPressed = Color.FromArgb(123,162,236), OnAccent = Color.FromArgb(16,31,60),
            Text = Color.FromArgb(244,246,251), TextMuted = Color.FromArgb(168,176,192),
            Danger = Color.FromArgb(255,157,165), Success = Color.FromArgb(122,215,177), Warn = Color.FromArgb(236,196,124)
        };
    }
`;
 s=s.slice(0,start)+palettes+s.slice(end);
 return s.replace('private const int ButtonRadius = 4;','private const int ButtonRadius = 10;').replace('g.ColumnHeadersHeight = Dpi.S(38)','g.ColumnHeadersHeight = Dpi.S(42)').replace('g.RowTemplate.Height = Dpi.S(36)','g.RowTemplate.Height = Dpi.S(42)');
});
edit('src/UI/Controls.cs',s=>s.replace('public int Radius = 8;','public int Radius = 18;').replace('Theme.RoundedRect(r, S(5))','Theme.RoundedRect(r, S(10))').replace('Theme.RoundedRect(outer, 6)','Theme.RoundedRect(outer, 10)').replace('Theme.RoundedRect(r, 5)','Theme.RoundedRect(r, 8)').replace('            if (_sel) using (var b = new SolidBrush(p.Accent)) using (var path = Theme.RoundedRect(new Rectangle(S(6), Height / 2 - S(8), S(3), S(16)), S(2))) g.FillPath(b, path);','').replace('new Rectangle(S(6), S(2), Width - S(12), Height - S(4))','new Rectangle(S(12), S(2), Width - S(24), Height - S(4))').replace('Theme.Font(22f, FontStyle.Bold)','Theme.Font(25f, FontStyle.Bold)'));
edit('src/MainForm.cs',s=>s.replace('Text = "Lumivolt 루미볼트  ·  © 음악천재"','Text = Brand.Name').replace('Ui.S(1240); Height = Ui.S(820)','Ui.S(1320); Height = Ui.S(860)').replace('Width = Ui.S(248)','Width = Ui.S(232)').replace('Height = Ui.S(60)','Height = Ui.S(70)').replace('Text = "Lumivolt", AutoSize = true, Font = Theme.Font(15f, FontStyle.Bold), Location = new Point(20, 18)','Text = Brand.Name, AutoSize = true, Font = Theme.Font(21f, FontStyle.Bold), Location = new Point(78, 28)').replace('Text = "마켓 자동등록 스튜디오"','Text = Brand.Tagline').replace('new Point(22, 48)','new Point(80, 65)').replace('sidebar.Controls.Add(lblBrand);','sidebar.Controls.Add(new BrandMark { Left = 24, Top = 28, Width = 42, Height = 42 });\n            sidebar.Controls.Add(lblBrand);').replace('"v" + ClientIdentityService.AppVersion + "  ·  © 음악천재"','"EXP  /  " + Brand.Version').replace('int y = 84;','int y = 126;').replace('else y += 42','else y += 46').replace('"dashboard", "Dashboard"','"dashboard", "한눈에 보기"').replace('lblPage.Text = "Lumivolt   ›   " + title;','lblPage.Text = title;').replace('_compact ? 68 : 248','_compact ? 76 : 232').replace('int y = Ui.S(84); int step = Ui.S(42)','int y = Ui.S(126); int step = Ui.S(46)').replace('            lblBrand.Visible = lblBrandSub.Visible = !_compact;','            lblBrand.Visible = lblBrandSub.Visible = !_compact;\n            foreach (Control c in sidebar.Controls) if (c is BrandMark) c.Visible = !_compact;').replace('            using (var p = new Pen(Theme.Current.Border)) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);',''));
edit('src/UI/UiKit.cs',s=>s.replace('return Text(text, 20f, true)','return Text(text, 22f, true)').replace('Padding = new Padding(28, 20, 28, 20)','Padding = new Padding(32, 26, 32, 26)'));
edit('src/UI/Pages/DashboardPage.cs',s=>s.replace('PageTitle = "Dashboard"','PageTitle = "한눈에 보기"').replace('Ui.Title("Dashboard")','Ui.Title("오늘의 판매, 한눈에.")').replace('현재 상품 목록과 실행 기록을 요약해서 보여 줍니다.','상품을 준비하고, 나머지는 EXP에 맡기세요.').replace('            Controls.AddRange(new Control[] { title, sub, grid, activity, recentLogs });','            var quick = Ui.Button("상품 준비하기", "\\uE710", BtnStyle.Primary, 150);\n            quick.Anchor = AnchorStyles.Top | AnchorStyles.Right;\n            quick.Click += delegate { Host.Navigate("products"); };\n            Controls.AddRange(new Control[] { title, sub, grid, activity, recentLogs, quick });\n            Resize += delegate { quick.Location = new Point(ClientSize.Width - Padding.Right - quick.Width, Padding.Top); };'));
edit('src/LicenseForm.cs',s=>{const a=s.indexOf('    public class LicenseForm : Form'); return s.slice(0,a)+`    public class LicenseForm : Form
    {
        public string Key = "";
        public LicenseForm(string machineId, string message)
        {
            Text = Brand.Name + " · 시작하기"; ClientSize = new Size(620, 620);
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; Font = Theme.FontBase();
            Controls.Add(new BrandMark { Left = 32, Top = 30, Width = 44, Height = 44 });
            Controls.Add(Design.Label(Brand.Name, 92, 30, 300, 34, 23, "t"));
            Controls.Add(Design.Label("한 번 인증하고, 바로 시작하세요.", 34, 88, 548, 30, 17, "t"));
            Controls.Add(Design.Label("파일 없이 복사와 붙여넣기만 하면 됩니다.", 34, 126, 548, 24, 10, "m"));
            var first = new SoftCard { Left = 32, Top = 174, Width = 556, Height = 148 };
            first.Controls.Add(Design.Label("01   등록코드를 판매자에게 보내기", 24, 20, 500, 28, 12, "t"));
            var request = Design.Input("requestCode", 24, 58, 318, 60, true); request.Text = "등록코드 복사를 눌러 주세요.";
            var copy = Design.Button("등록코드 복사", 358, 58, 172, true); copy.Name = "copyRequest";
            first.Controls.AddRange(new Control[] { request, copy });
            var second = new SoftCard { Left = 32, Top = 338, Width = 556, Height = 192 };
            second.Controls.Add(Design.Label("02   받은 인증키로 시작하기", 24, 20, 500, 28, 12, "t"));
            var input = Design.Input("licenseKey", 24, 58, 506, 66, false);
            var paste = Design.Button("인증키 붙여넣기", 24, 138, 174, false); paste.Name = "pasteLicense";
            var activate = Design.Button("인증하고 시작", 356, 138, 174, true); activate.Name = "activateLicense";
            var status = Design.Label(message, 34, 542, 552, 42, 9.5f, "m"); status.Name = "licenseStatus";
            var file = new LinkLabel { Text = "파일로 받은 경우", Left = 36, Top = 588, Width = 130, Font = Theme.Font(9f, FontStyle.Bold) };
            var close = new LinkLabel { Text = "닫기", Left = 544, Top = 588, Width = 50, Font = Theme.Font(9f, FontStyle.Bold) };
            close.LinkClicked += delegate { DialogResult = DialogResult.Cancel; };
            Action<string,bool> notice = (text, good) => { status.Text = text; status.Tag = good ? "s" : "d"; status.ForeColor = good ? Theme.Success : Theme.Danger; };
            copy.Click += delegate { string token, msg; bool ok = LicenseFiles.CopyRequest(out token, out msg); if (token.Length > 0) request.Text = token; notice(msg, ok); };
            paste.Click += delegate { try { input.Text = LicenseFiles.PasteKey(); notice("인증키를 붙여 넣었습니다. 인증하고 시작을 눌러 주세요.", true); } catch (Exception ex) { notice(ex is FormatException ? ex.Message : "클립보드를 읽지 못했습니다. 직접 붙여 넣어 주세요.", false); } };
            activate.Click += delegate { string msg; if (!LicenseGate.TryActivate(input.Text, out msg)) { notice(msg, false); return; } Key = Lumivolt.Security.LicenseText.Normalize(input.Text, "LV3"); DialogResult = DialogResult.OK; };
            file.LinkClicked += delegate { string token = LicenseFiles.ImportLicense(this); if (token != null) input.Text = token; };
            second.Controls.AddRange(new Control[] { input, paste, activate });
            Controls.AddRange(new Control[] { first, second, status, file, close });
            AcceptButton = activate; Design.Scale(this); Theme.Apply(this);
            file.LinkColor = close.LinkColor = Theme.Accent;
        }
    }
}
`;});
edit('src/LicenseForm.cs',s=>s.replace('                string result;\n                if (!TryActivate(f.Key, out result)) { FluentBox.Show(null, result, "라이선스"); return false; }\n                return true;','                return Recheck();').replace('                File.WriteAllText(KeyPath, key.Trim().Replace("\\r", "").Replace("\\n", "").Replace(" ", ""), Encoding.UTF8);','                File.WriteAllText(KeyPath, Lumivolt.Security.LicenseText.Normalize(key, "LV3"), Encoding.UTF8);'));
edit('src/UI/Pages/LicensePage.cs',s=>s.replace('Ui.Button("복사", "\\uE8C8", BtnStyle.Standard, 90)','Ui.Button("등록코드 복사", "\\uE8C8", BtnStyle.Primary, 150)').replace('new Point(430, 177)','new Point(490, 177)').replace('"등록 요청 저장"','"파일로 저장"').replace('            bCopy.Click += delegate { try { Clipboard.SetText(TpmDevice.DisplayId()); lblMsg.Text = "PC 고유번호를 복사했습니다."; lblMsg.Tag = "m"; lblMsg.ForeColor = Theme.TextMuted; } catch { } };','            bCopy.Click += delegate { string token, msg; bool ok = LicenseFiles.CopyRequest(out token, out msg); lblMsg.Text = msg; lblMsg.Tag = ok ? "s" : "d"; lblMsg.ForeColor = ok ? Theme.Success : Theme.Danger; if (!ok && token.Length > 0) { txtKey.Text = token; txtKey.SelectAll(); } Sync(); };').replace('Ui.Button("파일 열기", "", BtnStyle.Standard, 110)','Ui.Button("키 붙여넣기", "", BtnStyle.Standard, 120)').replace('bImport.Click += delegate { string token = LicenseFiles.ImportLicense(this); if (token != null) txtKey.Text = token; };','bImport.Click += delegate { try { txtKey.Text = LicenseFiles.PasteKey(); } catch (Exception ex) { lblMsg.Text = ex is FormatException ? ex.Message : "직접 붙여 넣어 주세요."; } };'));
for(const p of ['src/Terms.cs','src/Guard.cs','src/Program.cs','src/UI/Pages/SettingsPage.cs']) edit(p,s=>s.replace(/Lumivolt\(루미볼트\)|Lumivolt 루미볼트|Lumivolt/g,'EXP'));
edit('src/AssemblyInfo.cs',s=>s.replace(/루미볼트 Lumivolt|Lumivolt/g,'EXP').replace(/2\.1\.0/g,'2.2.0'));
edit('Lumivolt.csproj',s=>s.replace('<AssemblyName>Lumivolt</AssemblyName>','<AssemblyName>EXP</AssemblyName>'));
edit('build/Build.ps1',s=>s.replace("'src\\Theme.cs'","'src\\Theme.cs','src\\Brand.cs','src\\Design.cs','src\\Security\\TrustedKeys.cs','admin\\AdminWorkflow.cs'").replace("Compile 'LicenseAdmin'","Compile 'EXP-Studio'").replace("Compile 'Lumivolt'","Compile 'EXP'"));
