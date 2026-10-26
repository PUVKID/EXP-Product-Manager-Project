using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>설정: 테마, Chrome 경로, 데이터/로그 폴더, 운영 로그 전송 상태, 자가 진단, 관리자 로그인.</summary>
    public class SettingsPage : PageBase
    {
        private readonly Segmented seg;
        private readonly TextBox txtChrome;
        private readonly Label lblTele, lblClient;
        private readonly Timer _t = new Timer { Interval = 2000 };
        private bool _loading;

        public SettingsPage(AppHost host) : base(host)
        {
            Key = "settings"; PageTitle = "설정";
            var title = Ui.Title("설정"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("테마와 프로그램 환경을 바꿉니다."); sub.Location = new Point(2, 38);

            // ---- 화면
            var c1 = new CardPanel { Location = new Point(0, 74), Height = 120, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h1 = Ui.Heading("화면 테마"); h1.Location = new Point(20, 16);
            var d1 = Ui.Muted("시스템을 고르면 Windows 의 앱 모드 설정을 따라갑니다."); d1.Location = new Point(22, 46);
            seg = new Segmented { Width = 300, Location = new Point(20, 74) };
            seg.Items.AddRange(new[] { "라이트", "다크", "시스템" });
            c1.Controls.AddRange(new Control[] { h1, d1, seg });

            // ---- Chrome / 폴더
            var c2 = new CardPanel { Location = new Point(0, 206), Height = 190, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h2 = Ui.Heading("Chrome 및 폴더"); h2.Location = new Point(20, 16);
            var d2 = Ui.Muted("Chrome 을 자동으로 못 찾을 때만 chrome.exe 위치를 지정하세요. 비워 두면 자동으로 찾습니다."); d2.Location = new Point(22, 46);
            txtChrome = Ui.Input(460, Host.Cfg.ChromePath ?? ""); txtChrome.Location = new Point(22, 76);
            var bBrowse = Ui.Button("찾아보기", "", BtnStyle.Standard, 110); bBrowse.Location = new Point(496, 72);
            var bData = Ui.Button("데이터 폴더 열기", "\uE838", BtnStyle.Standard, 170); bData.Location = new Point(20, 124);
            var bLogs = Ui.Button("로그 폴더 열기", "\uE838", BtnStyle.Standard, 160); bLogs.Location = new Point(198, 124);
            c2.Controls.AddRange(new Control[] { h2, d2, txtChrome, bBrowse, bData, bLogs });

            // ---- 운영 로그 전송
            var c3 = new CardPanel { Location = new Point(0, 408), Height = 190, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h3 = Ui.Heading("운영 로그 전송"); h3.Location = new Point(20, 16);
            lblTele = Ui.Text("", 10f, true); lblTele.Location = new Point(22, 46);
            lblClient = Ui.Muted(""); lblClient.Location = new Point(22, 72);
            var d3 = Ui.Muted(
                "전송하는 것: 프로그램 운영 로그, 오류 정보, 프로그램/Windows 버전, CPU·메모리·그래픽·화면 정보, 기능 사용 횟수.\r\n" +
                "전송하지 않는 것: 비밀번호, 쿠키, 세션·OAuth 토큰, 인증 정보, 개인키, 카드·계좌 정보. (로그에 섞인 값은 보내기 전에 자동으로 지웁니다.)\r\n" +
                "인터넷이 없어도 프로그램은 정상 동작하며, 못 보낸 기록은 연결되면 전송됩니다. (이용약관 제7조)");
            d3.Location = new Point(22, 100);
            c3.Controls.AddRange(new Control[] { h3, lblTele, lblClient, d3 });

            // ---- 도구
            var c4 = new CardPanel { Location = new Point(0, 610), Height = 150, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h4 = Ui.Heading("도구"); h4.Location = new Point(20, 16);
            var bSelf = Ui.Button("자가 진단 실행", "\uE9D9", BtnStyle.Standard, 180); bSelf.Location = new Point(20, 52);
            var bAdmin = Ui.Button("관리자 로그인", "\uE716", BtnStyle.Standard, 170); bAdmin.Location = new Point(212, 52);
            var d4 = Ui.Muted("v" + ClientIdentityService.AppVersion + "  ·  Copyright © 음악천재. All rights reserved."); d4.Location = new Point(22, 106);
            c4.Controls.AddRange(new Control[] { h4, bSelf, bAdmin, d4 });

            Controls.AddRange(new Control[] { title, sub, c1, c2, c3, c4 });
            Resize += delegate { int w = Math.Max(400, ClientSize.Width - Padding.Horizontal); c1.Width = w; c2.Width = w; c3.Width = w; c4.Width = w; };

            seg.SelectedIndexChanged += delegate { if (!_loading) ThemeService.SetMode((ThemeMode)seg.SelectedIndex); };
            txtChrome.Leave += delegate { SaveChrome(); };
            bBrowse.Click += delegate
            {
                using (var d = new OpenFileDialog { Filter = "chrome.exe|chrome.exe|실행 파일|*.exe" })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK) { txtChrome.Text = d.FileName; SaveChrome(); }
            };
            bData.Click += delegate { Ui.OpenFolder(Paths.Root); };
            bLogs.Click += delegate { Ui.OpenFolder(Paths.Logs); };
            bSelf.Click += delegate { RunSelfTest(bSelf); };
            bAdmin.Click += delegate { AdminLogin(); };
            _t.Tick += delegate { if (Visible) SyncTele(); };
        }

        private void SaveChrome()
        {
            string v = txtChrome.Text.Trim();
            if (v == (Host.Cfg.ChromePath ?? "")) return;
            Host.Cfg.ChromePath = v; Storage.SaveSettings(Host.Cfg);
            Log.Write("INFO", "Settings", "settings.change", "Chrome 경로 설정을 바꿨습니다." + (v.Length == 0 ? " (자동 탐색)" : ""), "OK");
        }

        private void SyncTele()
        {
            var cfg = TelemetryService.Config;
            if (cfg == null || !cfg.Enabled) { lblTele.Text = "꺼짐"; lblTele.Tag = "m"; }
            else if (!cfg.UrlAllowed || string.IsNullOrWhiteSpace(cfg.IngestKey)) { lblTele.Text = "서버 미설정 — 전송하지 않습니다 (telemetry.json 에서 설정)"; lblTele.Tag = "m"; }
            else
            {
                var c = TelemetryService.Central;
                bool ok = c != null && c.Online;
                lblTele.Text = ok ? "전송 중 (정상)" : "대기 중" + (c != null && c.LastError.Length > 0 ? " — 마지막 오류: " + c.LastError : "");
                lblTele.Tag = ok ? "s" : "m";
            }
            lblTele.ForeColor = (string)lblTele.Tag == "s" ? Theme.Success : Theme.TextMuted;
            lblClient.Text = "Client ID: " + ClientIdentityService.ClientId + (TelemetryService.Queue != null ? "   ·   대기 중인 기록 " + TelemetryService.Queue.Count + "건" : "");
        }

        private void RunSelfTest(FluentButton b)
        {
            b.Enabled = false; b.Text = "진단 중...";
            var ui = this;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var sw = new StringWriter(); var old = Console.Out; int code;
                try { Console.SetOut(sw); code = SelfTest.Run(); }
                catch (Exception ex) { sw.WriteLine("진단 중 예외: " + ex.Message); code = 1; }
                finally { Console.SetOut(old); }
                Log.Write(code == 0 ? "INFO" : "ERROR", "SelfTest", "selftest.result", "자가 진단 결과: " + (code == 0 ? "모두 통과" : "실패 있음"), code == 0 ? "OK" : "FAIL");
                try { ui.BeginInvoke((Action)delegate { b.Enabled = true; b.Text = "자가 진단 실행"; ShowText("자가 진단 결과", sw.ToString()); }); } catch { }
            });
        }

        private void ShowText(string title, string text)
        {
            using (var f = new Form { Text = title, Width = 720, Height = 640, StartPosition = FormStartPosition.CenterParent })
            {
                var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = Theme.FontMono(9.5f), WordWrap = false, Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n") };
                f.Controls.Add(box); Theme.Apply(f); box.Select(0, 0);
                f.ShowDialog(FindForm());
            }
        }

        private void AdminLogin()
        {
            var cfg = TelemetryService.Config;
            if (cfg == null || !cfg.UrlAllowed) { FluentBox.Show(FindForm(), "중앙 로그 서버 주소가 설정되어 있지 않습니다.\r\n(telemetry.json 의 ServerUrl)", "관리자 로그인", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var f = new AdminLoginForm(cfg.ServerUrl))
            {
                if (f.ShowDialog(FindForm()) == DialogResult.OK && f.Client != null && f.Client.IsAdmin)
                {
                    Host.SetAdmin(f.Client);
                    Log.Write("INFO", "Admin", "admin.login", "관리자 로그인에 성공했습니다.", "OK");
                    Host.Navigate("admin");
                }
            }
        }

        public override void OnActivated()
        {
            _loading = true; seg.SetIndexSilently((int)ThemeService.Mode); _loading = false;
            txtChrome.Text = Host.Cfg.ChromePath ?? "";
            SyncTele(); _t.Start();
        }
        public override void OnDeactivated() { _t.Stop(); }
        public override void OnThemeChanged() { SyncTele(); }
    }

    /// <summary>관리자 아이디/비밀번호는 서버에서만 확인한다. 이 창은 입력값을 서버로 보내고 세션 토큰만 받는다(비밀번호는 저장하지 않음).</summary>
    public class AdminLoginForm : Form
    {
        public AdminApiClient Client;
        public AdminLoginForm(string serverUrl)
        {
            Text = "관리자 로그인"; Width = 440; Height = 330; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; Font = Theme.FontBase();
            var h = Theme.BuildHeader("관리자 로그인", "서버에서 관리자 권한을 확인합니다");
            var lu = new Label { Text = "아이디", Left = 24, Top = 82, AutoSize = true };
            var tu = new TextBox { Left = 24, Top = 104, Width = 376 };
            var lp = new Label { Text = "비밀번호", Left = 24, Top = 140, AutoSize = true };
            var tp = new TextBox { Left = 24, Top = 162, Width = 376, UseSystemPasswordChar = true };
            var err = new Label { Left = 24, Top = 198, Width = 376, Height = 36, ForeColor = Theme.Danger, Tag = "d" };
            var ok = new Button { Text = "로그인", Left = 214, Top = 244, Width = 90, Height = 34 };
            var no = new Button { Text = "취소", Left = 310, Top = 244, Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { lu, tu, lp, tp, err, ok, no, h });
            CancelButton = no; AcceptButton = ok;
            Theme.MarkPrimary(ok); Theme.Apply(this);
            ok.Click += delegate
            {
                ok.Enabled = false; err.Text = "확인하는 중...";
                string u = tu.Text, p = tp.Text;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    var c = new AdminApiClient(serverUrl); bool good = false;
                    try { good = c.Login(u, p); } catch (Exception ex) { c.LastError = ex.Message; }
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            ok.Enabled = true;
                            if (good) { Client = c; tp.Text = ""; DialogResult = DialogResult.OK; Close(); }
                            else err.Text = c.LastError.Length > 0 ? c.LastError : "관리자 권한이 없습니다.";
                        });
                    }
                    catch { }
                });
            };
        }
    }
}
