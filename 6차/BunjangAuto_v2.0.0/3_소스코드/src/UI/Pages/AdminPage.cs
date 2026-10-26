using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    /// <summary>
    /// 관리자 센터(ADMIN 로 로그인했을 때만 사이드바에 나타난다). 데이터는 전부 중앙 서버의 관리자 API 에서 가져오며,
    /// 이 EXE 에는 관리자 비밀번호나 서버 비밀 값이 들어 있지 않다.
    /// 탭: Dashboard / Users / Live Logs / Errors / Statistics
    /// </summary>
    public class AdminPage : PageBase
    {
        private readonly Segmented tabs;
        private readonly Panel body;
        private readonly Label lblInfo;
        private readonly Timer _live = new Timer { Interval = 3000 };
        private int _lastId; private bool _liveBusy;
        private DataGridView _liveGrid; private List<JObject> _liveRows = new List<JObject>();
        private Func<IDictionary<string, string>> _liveFilter;

        public AdminPage(AppHost host) : base(host)
        {
            Key = "admin"; PageTitle = "관리자 센터";
            AutoScroll = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.BgDark };
            var title = Ui.Title("관리자 센터"); title.Location = new Point(0, 0);
            lblInfo = Ui.Muted(""); lblInfo.Location = new Point(2, 38);
            tabs = new Segmented { Location = new Point(0, 70), Width = 560, Height = 34 };
            tabs.Items.AddRange(new[] { "Dashboard", "Users", "Live Logs", "Errors", "Statistics" });
            var bOut = Ui.Button("로그아웃", "\uE7E8", BtnStyle.Standard, 120); bOut.Location = new Point(580, 69); bOut.Height = 34;
            top.Controls.AddRange(new Control[] { title, lblInfo, tabs, bOut });
            body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgDark };
            Controls.Add(body); Controls.Add(top);
            tabs.SelectedIndexChanged += delegate { ShowTab(); };
            bOut.Click += delegate { if (Host.Admin != null) Host.Admin.Logout(); Host.SetAdmin(null); Host.Navigate("dashboard"); };
            _live.Tick += delegate { PollLive(false); };
        }

        public override void OnActivated()
        {
            lblInfo.Text = "서버: " + (TelemetryService.Config != null ? TelemetryService.Config.ServerUrl : "") + "   ·   역할: ADMIN";
            ShowTab();
        }
        public override void OnDeactivated() { _live.Stop(); }
        public override void OnThemeChanged() { ShowTab(); }

        private AdminApiClient Api { get { return Host.Admin; } }

        private void ShowTab()
        {
            _live.Stop();
            body.SuspendLayout(); body.Controls.Clear();
            if (Api == null || !Api.IsAdmin) { body.Controls.Add(Ui.Muted("관리자 로그인이 필요합니다. (설정 > 관리자 로그인)")); body.ResumeLayout(); return; }
            switch (tabs.SelectedIndex)
            {
                case 0: BuildDashboard(); break;
                case 1: BuildUsers(); break;
                case 2: BuildLive(); break;
                case 3: BuildErrors(); break;
                default: BuildStats(); break;
            }
            body.ResumeLayout();
        }

        private static string S(JToken t, string k) { var v = t[k]; return v == null || v.Type == JTokenType.Null ? "" : v.ToString(); }
        private static string Local(string iso)
        {
            DateTime d;
            if (string.IsNullOrEmpty(iso)) return "-";
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d) ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : iso;
        }

        private DataGridView MakeGrid(params string[][] cols)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false, AllowUserToResizeRows = false };
            foreach (var c in cols)
            {
                var col = new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1] };
                if (c.Length > 2 && c[2] == "fill") { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; col.MinimumWidth = 200; } else col.Width = c.Length > 2 ? int.Parse(c[2]) : 110;
                g.Columns.Add(col);
            }
            Theme.StyleGrid(g);
            g.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.Value == null) return; string v = e.Value.ToString();
                if (v == "ERROR" || v == "FATAL" || v == "FAIL") e.CellStyle.ForeColor = Theme.Danger; else if (v == "WARN") e.CellStyle.ForeColor = Theme.Warn; else if (v == "OK" || v == "온라인") e.CellStyle.ForeColor = Theme.Success;
            };
            return g;
        }

        private void Fail(Control host, string msg) { host.Controls.Clear(); var l = Ui.Text(msg, 10f, false, "d"); l.Location = new Point(0, 8); host.Controls.Add(l); }

        // ============================ Dashboard ============================
        private void BuildDashboard()
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgDark }; body.Controls.Add(host);
            var wait = Ui.Muted("불러오는 중..."); host.Controls.Add(wait);
            Ui.Background(this, () => new[] { Api.Overview(), Api.Stats(7) }, r =>
            {
                host.Controls.Clear();
                var o = r == null ? null : r[0]; var st = r == null ? null : r[1];
                if (o == null || st == null) { Fail(host, "불러오지 못했습니다: " + Api.LastError); return; }
                var grid = new TableLayoutPanel { Location = new Point(0, 0), ColumnCount = 4, RowCount = 2, Height = 216, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Theme.BgDark };
                for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                Action<string, string, Func<Color>> add = (cap, val, tone) => { var c = new StatCard { Caption = cap, Value = val, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12), Tone = tone }; grid.Controls.Add(c); };
                add("전체 설치 수", S(o, "total_installs"), null); add("활성 사용자(7일)", S(o, "active_users"), null);
                add("현재 온라인", S(o, "online_users"), () => Theme.Success); add("오늘 실행 수", S(o, "today_runs"), null);
                add("오늘 자동등록 수", S(o, "today_registrations"), null); add("오늘 성공 수", S(o, "today_success"), () => Theme.Success);
                add("오늘 실패 수", S(o, "today_fail"), () => Theme.Danger); add("오늘 오류 수", S(o, "today_errors"), () => Theme.Danger);
                host.Controls.Add(grid);
                var series = (JArray)st["series"];
                var labels = series.Select(x => S(x, "day").Substring(5)).ToList();
                Func<string, double[]> col = k => series.Select(x => (double)x[k]).ToArray();
                var ch1 = Chart("최근 7일 실행량", labels, new[] { col("runs") }, new Func<Color>[] { () => Theme.Accent });
                var ch2 = Chart("최근 7일 등록 성공/실패", labels, new[] { col("success"), col("fail") }, new Func<Color>[] { () => Theme.Success, () => Theme.Danger });
                var ch3 = Chart("최근 7일 오류", labels, new[] { col("errors") }, new Func<Color>[] { () => Theme.Danger });
                host.Controls.AddRange(new Control[] { ch1, ch2, ch3 });
                Action lay = () =>
                {
                    int w = Math.Max(480, host.ClientSize.Width - 18); grid.SetBounds(0, 0, w, 216);
                    int half = (w - 12) / 2; ch1.SetBounds(0, 228, half, 230); ch2.SetBounds(half + 12, 228, w - half - 12, 230); ch3.SetBounds(0, 470, half, 230);
                };
                host.Resize += delegate { lay(); }; lay();
            });
        }

        private CardPanel Chart(string title, List<string> labels, double[][] data, Func<Color>[] colors)
        {
            var card = new CardPanel();
            var h = Ui.Heading(title); h.Location = new Point(16, 12);
            var bc = new BarChart { Location = new Point(12, 44), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            bc.Labels = labels; bc.Series = data.ToList(); bc.Colors = colors.ToList();
            card.Controls.Add(h); card.Controls.Add(bc);
            card.Resize += delegate { bc.SetBounds(12, 44, card.Width - 24, card.Height - 56); };
            return card;
        }

        // ============================ Users ============================
        private void BuildUsers()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.BgDark };
            var q = Ui.Input(250); q.Location = new Point(0, 8);
            var ver = Ui.Input(100); ver.Location = new Point(260, 8);
            var on = Ui.Toggle("온라인만"); on.Location = new Point(372, 8); on.Width = 130;
            var er = Ui.Toggle("오류 발생"); er.Location = new Point(510, 8); er.Width = 130;
            var recent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Location = new Point(650, 7), Font = Theme.FontBase() };
            recent.Items.AddRange(new object[] { "최근 접속: 전체", "1시간 이내", "24시간 이내", "7일 이내" }); recent.SelectedIndex = 0;
            var go = Ui.Button("검색", "\uE721", BtnStyle.Primary, 90); go.Location = new Point(790, 5);
            bar.Controls.AddRange(new Control[] { q, ver, on, er, recent, go });
            var hint = Ui.Muted("검색: Client ID / 이름 / 버전 / OS   ·   버전 칸에 2.0.0.0 처럼 입력하면 그 버전만 봅니다"); hint.Location = new Point(2, 36);
            bar.Controls.Add(hint); bar.Height = 64;
            var g = MakeGrid(new[] { "id", "Client ID", "250" }, new[] { "name", "이름", "110" }, new[] { "ver", "버전", "80" }, new[] { "os", "OS", "fill" },
                new[] { "on", "상태", "70" }, new[] { "seen", "마지막 접속", "140" }, new[] { "n1", "실행", "56" }, new[] { "n2", "자동등록", "70" }, new[] { "n3", "성공", "56" }, new[] { "n4", "실패", "56" }, new[] { "n5", "오류", "56" });
            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) }; card.Controls.Add(g);
            body.Controls.Add(card); body.Controls.Add(bar);
            Action load = () =>
            {
                var f = new Dictionary<string, string> { { "q", q.Text.Trim() }, { "version", ver.Text.Trim() }, { "online", on.Checked ? "1" : "" }, { "has_error", er.Checked ? "1" : "" } };
                string[] hrs = { "", "1", "24", "168" }; f["recent_hours"] = hrs[Math.Max(0, recent.SelectedIndex)];
                Ui.Background(this, () => Api.Users(f), r =>
                {
                    g.Rows.Clear(); if (r == null) { FluentBox.Show(FindForm(), "불러오지 못했습니다: " + Api.LastError, "관리자 센터"); return; }
                    foreach (var u in (JArray)r["users"])
                        g.Rows.Add(S(u, "client_id"), S(u, "license_name"), S(u, "app_version"), S(u, "os"), (bool)u["online"] ? "온라인" : "오프라인", Local(S(u, "last_seen")), S(u, "launches"), S(u, "auto_runs"), S(u, "success"), S(u, "fail"), S(u, "errors"));
                });
            };
            go.Click += delegate { load(); };
            on.CheckedChanged += delegate { load(); }; er.CheckedChanged += delegate { load(); }; recent.SelectedIndexChanged += delegate { load(); };
            q.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) load(); };
            g.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) OpenDetail(Convert.ToString(g.Rows[e.RowIndex].Cells["id"].Value)); };
            g.CellClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex == 0) OpenDetail(Convert.ToString(g.Rows[e.RowIndex].Cells["id"].Value)); };
            load();
        }

        private void OpenDetail(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return;
            using (var f = new AdminUserForm(Api, clientId)) f.ShowDialog(FindForm());
        }

        // ============================ Live Logs ============================
        private void BuildLive()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.BgDark };
            var q = Ui.Input(220); q.Location = new Point(0, 8);
            var cid = Ui.Input(250); cid.Location = new Point(230, 8);
            var lv = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Location = new Point(490, 7), Font = Theme.FontBase() };
            lv.Items.AddRange(new object[] { "전체 Level", "ERROR,FATAL", "WARN", "INFO" }); lv.SelectedIndex = 0;
            var pause = Ui.Toggle("실시간", true); pause.Location = new Point(620, 8); pause.Width = 120;
            var bCsv = Ui.Button("CSV", "\uE74E", BtnStyle.Standard, 80); bCsv.Location = new Point(750, 5);
            var bJson = Ui.Button("JSON", "\uE74E", BtnStyle.Standard, 90); bJson.Location = new Point(838, 5);
            var ph = Ui.Muted("검색어 / Client ID 전체 입력 / Level"); ph.Location = new Point(2, 36);
            bar.Controls.AddRange(new Control[] { q, cid, lv, pause, bCsv, bJson, ph }); bar.Height = 64;
            var g = MakeGrid(new[] { "t", "시간", "140" }, new[] { "c", "Client", "80" }, new[] { "l", "Level", "70" }, new[] { "m", "Module", "110" }, new[] { "a", "Action", "150" }, new[] { "msg", "Message", "fill" }, new[] { "r", "Result", "60" });
            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) }; card.Controls.Add(g);
            body.Controls.Add(card); body.Controls.Add(bar);
            _liveGrid = g; _liveRows = new List<JObject>(); _lastId = 0;
            Func<IDictionary<string, string>> filt = () => new Dictionary<string, string> { { "q", q.Text.Trim() }, { "client_id", cid.Text.Trim() }, { "level", lv.SelectedIndex == 0 ? "" : (string)lv.SelectedItem } };
            _liveFilter = filt;
            Action reset = () => { _liveRows.Clear(); _lastId = 0; g.Rows.Clear(); PollLive(true); };
            q.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) reset(); };
            cid.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) reset(); };
            lv.SelectedIndexChanged += delegate { reset(); };
            pause.CheckedChanged += delegate { if (pause.Checked) _live.Start(); else _live.Stop(); };
            bCsv.Click += delegate { ExportLogs(filt(), "csv"); }; bJson.Click += delegate { ExportLogs(filt(), "json"); };
            PollLive(true); _live.Start();
        }

        private void PollLive(bool first)
        {
            if (_liveBusy || _liveGrid == null || _liveGrid.IsDisposed || Api == null) return;
            _liveBusy = true;
            var f = new Dictionary<string, string>(_liveFilter());
            f["limit"] = first ? "150" : "500"; if (_lastId > 0) f["since_id"] = _lastId.ToString();
            Ui.Background(this, () => Api.Logs(f), r =>
            {
                _liveBusy = false;
                if (r == null || _liveGrid.IsDisposed) return;
                var ev = ((JArray)r["events"]).Cast<JObject>().ToList();       // 최신순
                if (ev.Count == 0) return;
                _lastId = Math.Max(_lastId, ev.Max(x => (int)x["id"]));
                for (int i = ev.Count - 1; i >= 0; i--) { var e = ev[i]; _liveGrid.Rows.Insert(0, Local(S(e, "ts")), S(e, "client_id").Substring(0, 8), S(e, "level"), S(e, "module"), S(e, "action"), S(e, "message").Replace("\n", " "), S(e, "result")); }
                while (_liveGrid.Rows.Count > 1000) _liveGrid.Rows.RemoveAt(_liveGrid.Rows.Count - 1);
            });
        }

        // ============================ Errors ============================
        private void BuildErrors()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.BgDark };
            var bR = Ui.Button("새로고침", "\uE72C", BtnStyle.Primary, 120); bR.Location = new Point(0, 6);
            var bCsv = Ui.Button("CSV", "\uE74E", BtnStyle.Standard, 80); bCsv.Location = new Point(130, 6);
            var bJson = Ui.Button("JSON", "\uE74E", BtnStyle.Standard, 90); bJson.Location = new Point(218, 6);
            bar.Controls.AddRange(new Control[] { bR, bCsv, bJson });
            var g = MakeGrid(new[] { "t", "발생시간", "140" }, new[] { "c", "Client ID", "250" }, new[] { "v", "Version", "80" }, new[] { "m", "Module", "110" }, new[] { "x", "Exception Type", "200" }, new[] { "msg", "Message", "fill" });
            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) }; card.Controls.Add(g);
            body.Controls.Add(card); body.Controls.Add(bar);
            Action load = () => Ui.Background(this, () => Api.Errors(300), r =>
            {
                g.Rows.Clear(); if (r == null) { FluentBox.Show(FindForm(), "불러오지 못했습니다: " + Api.LastError, "관리자 센터"); return; }
                foreach (var e in (JArray)r["events"]) g.Rows.Add(Local(S(e, "ts")), S(e, "client_id"), S(e, "version"), S(e, "module"), S(e, "exc_type"), S(e, "message").Replace("\n", " "));
            });
            bR.Click += delegate { load(); };
            var ef = new Dictionary<string, string> { { "level", "ERROR,FATAL" } };
            bCsv.Click += delegate { ExportLogs(ef, "csv"); }; bJson.Click += delegate { ExportLogs(ef, "json"); };
            g.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) OpenDetail(Convert.ToString(g.Rows[e.RowIndex].Cells["c"].Value)); };
            load();
        }

        // ============================ Statistics ============================
        private void BuildStats()
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgDark }; body.Controls.Add(host);
            host.Controls.Add(Ui.Muted("불러오는 중..."));
            Ui.Background(this, () => Api.Stats(14), st =>
            {
                host.Controls.Clear();
                if (st == null) { Fail(host, "불러오지 못했습니다: " + Api.LastError); return; }
                var series = (JArray)st["series"]; var labels = series.Select(x => S(x, "day").Substring(5)).ToList();
                Func<string, double[]> col = k => series.Select(x => (double)x[k]).ToArray();
                var c1 = Chart("최근 14일 실행량", labels, new[] { col("runs") }, new Func<Color>[] { () => Theme.Accent });
                var c2 = Chart("등록 성공/실패", labels, new[] { col("success"), col("fail") }, new Func<Color>[] { () => Theme.Success, () => Theme.Danger });
                var c3 = Chart("오류", labels, new[] { col("errors") }, new Func<Color>[] { () => Theme.Danger });
                var l1 = ListCard("버전별 사용자", (JArray)st["versions"]); var l2 = ListCard("OS별 사용자", (JArray)st["os"]); var l3 = ListCard("기능별 사용량", (JArray)st["features"]);
                host.Controls.AddRange(new Control[] { c1, c2, c3, l1, l2, l3 });
                Action lay = () =>
                {
                    int w = Math.Max(480, host.ClientSize.Width - 18); int half = (w - 12) / 2;
                    c1.SetBounds(0, 0, half, 230); c2.SetBounds(half + 12, 0, w - half - 12, 230);
                    c3.SetBounds(0, 242, half, 230); l3.SetBounds(half + 12, 242, w - half - 12, 230);
                    l1.SetBounds(0, 484, half, 260); l2.SetBounds(half + 12, 484, w - half - 12, 260);
                };
                host.Resize += delegate { lay(); }; lay();
            });
        }

        private CardPanel ListCard(string title, JArray items)
        {
            var card = new CardPanel(); var h = Ui.Heading(title); h.Location = new Point(16, 12); card.Controls.Add(h);
            var g = MakeGrid(new[] { "n", "이름", "fill" }, new[] { "v", "수", "70" });
            g.Location = new Point(8, 44); g.Dock = DockStyle.None; g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            foreach (var i in items) g.Rows.Add(S(i, "name"), S(i, "n"));
            card.Controls.Add(g); card.Resize += delegate { g.SetBounds(8, 44, card.Width - 16, card.Height - 52); };
            return card;
        }

        // ============================ 공용: 내보내기 ============================
        private void ExportLogs(IDictionary<string, string> filter, string fmt)
        {
            using (var d = new SaveFileDialog { Filter = fmt == "csv" ? "CSV|*.csv" : "JSON|*.json", FileName = "bja-logs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "." + fmt })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                string path = d.FileName;
                Ui.Background(this, () => Api.Export(filter, fmt), text =>
                {
                    if (text == null) { FluentBox.Show(FindForm(), "내보내기 실패: " + Api.LastError, "관리자 센터"); return; }
                    try { File.WriteAllText(path, text, new System.Text.UTF8Encoding(fmt == "csv" ? false : false)); Log.Write("INFO", "Admin", "admin.export", "관리자 로그를 " + fmt.ToUpper() + " 로 내보냈습니다.", "OK"); FluentBox.Show(FindForm(), "저장했습니다:\r\n" + path, "내보내기"); }
                    catch (Exception ex) { FluentBox.Show(FindForm(), "저장 실패: " + ex.Message, "오류"); }
                });
            }
        }
    }

    /// <summary>사용자(설치) 상세: 시스템 정보, 통계, 최근 오류, 이 사용자만의 로그.</summary>
    public class AdminUserForm : Form
    {
        public AdminUserForm(AdminApiClient api, string clientId)
        {
            Text = "사용자 상세 — " + clientId; Width = 1100; Height = 780; StartPosition = FormStartPosition.CenterParent; Font = Theme.FontBase();
            var info = new TextBox { Dock = DockStyle.Top, Height = 190, Multiline = true, ReadOnly = true, Font = Theme.FontMono(9.5f), BorderStyle = BorderStyle.None, Text = "불러오는 중..." };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tErr = new TabPage("최근 오류"); var tLog = new TabPage("최근 로그");
            var gErr = Grid(); var gLog = Grid(); tErr.Controls.Add(gErr); tLog.Controls.Add(gLog); tabs.TabPages.Add(tErr); tabs.TabPages.Add(tLog);
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10, 8, 0, 0) };
            var bCsv = new Button { Text = "이 사용자 로그 CSV", Width = 170, Height = 34 }; var bJson = new Button { Text = "JSON", Width = 90, Height = 34 };
            bar.Controls.Add(bCsv); bar.Controls.Add(bJson);
            Controls.Add(tabs); Controls.Add(bar); Controls.Add(info);
            Theme.Apply(this);
            Action<string> export = fmt =>
            {
                using (var d = new SaveFileDialog { Filter = fmt == "csv" ? "CSV|*.csv" : "JSON|*.json", FileName = "bja-" + clientId.Substring(0, 8) + "." + fmt })
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        string p = d.FileName;
                        Ui.Background(this, () => api.Export(new Dictionary<string, string> { { "client_id", clientId } }, fmt), t => { if (t != null) File.WriteAllText(p, t, new System.Text.UTF8Encoding(false)); else FluentBox.Show(this, "실패: " + api.LastError, "오류"); });
                    }
            };
            bCsv.Click += delegate { export("csv"); }; bJson.Click += delegate { export("json"); };
            Ui.Background(this, () => api.UserDetail(clientId), r =>
            {
                if (r == null) { info.Text = "불러오지 못했습니다: " + api.LastError; return; }
                var c = r["client"]; var sb = new System.Text.StringBuilder();
                Action<string, string> L = (k, v) => sb.AppendLine((k + "                ").Substring(0, 16) + v);
                L("Client ID", S(c, "client_id")); L("이름(라이선스)", S(c, "license_name")); L("PC 고유번호", S(c, "pc_id")); L("앱 버전", S(c, "app_version"));
                L("Windows", S(c, "os")); L("CPU", S(c, "cpu")); L("RAM", S(c, "ram_mb") + " MB"); L("GPU", S(c, "gpu")); L("해상도/DPI", S(c, "screen") + " / " + S(c, "dpi"));
                L("마지막 실행", Loc(S(c, "last_start"))); L("마지막 접속", Loc(S(c, "last_seen"))); L("온라인", (bool)c["online"] ? "온라인" : "오프라인");
                L("실행/자동등록", S(c, "launches") + " / " + S(c, "auto_runs") + "   성공 " + S(c, "success") + "  실패 " + S(c, "fail") + "  오류 " + S(c, "errors"));
                info.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
                Fill(gErr, (JArray)r["recent_errors"]); Fill(gLog, (JArray)r["recent_logs"]);
            });
        }
        private static string S(JToken t, string k) { var v = t[k]; return v == null || v.Type == JTokenType.Null ? "" : v.ToString(); }
        private static string Loc(string iso) { DateTime d; return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d) ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : iso; }
        private static DataGridView Grid()
        {
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시간", Width = 140 }); g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Level", Width = 70 });
            g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Module", Width = 110 }); g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", Width = 150 });
            g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill }); g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", Width = 60 });
            Theme.StyleGrid(g); return g;
        }
        private static void Fill(DataGridView g, JArray a) { g.Rows.Clear(); foreach (var e in a) g.Rows.Add(Loc(S(e, "ts")), S(e, "level"), S(e, "module"), S(e, "action"), S(e, "message").Replace("\n", " "), S(e, "result")); }
    }
}
