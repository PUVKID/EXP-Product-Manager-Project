using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>실제 상품 목록·라이선스·로그인 상태·사용 기록으로 채우는 요약 화면. 처음 실행해도 비어 보이지 않도록 모든 카드에 안내 문구를 둔다.</summary>
    public class DashboardPage : PageBase
    {
        private readonly StatCard cPending, cDone, cFail, cTotal, cLogin, cLic, cExp, cLeft;
        private readonly CardPanel activity, recentLogs;
        private readonly FlatList lstActivity, lstLogs;
        private readonly Timer _t = new Timer { Interval = 2000 };
        private readonly TableLayoutPanel grid;
        private readonly FluentButton more;

        public DashboardPage(AppHost host) : base(host)
        {
            Key = "dashboard"; PageTitle = "Dashboard";
            var title = Ui.Title("Dashboard"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("현재 상품 목록과 실행 기록을 요약해서 보여 줍니다."); sub.Location = new Point(2, 38);

            grid = new TableLayoutPanel { Location = new Point(0, 74), ColumnCount = 4, RowCount = 2, Height = 216, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Theme.BgDark };
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Func<string, StatCard> mk = cap => { var c = new StatCard { Caption = cap, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12) }; grid.Controls.Add(c); return c; };
            cPending = mk("등록 대기"); cDone = mk("등록 성공"); cFail = mk("등록 실패"); cTotal = mk("전체 상품");
            cLogin = mk("현재 로그인 상태"); cLic = mk("라이선스 상태"); cExp = mk("라이선스 만료일"); cLeft = mk("남은 기간");
            cDone.Tone = () => Theme.Success; cFail.Tone = () => Theme.Danger;

            activity = new CardPanel { Location = new Point(0, 300), Height = 300, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            activity.Controls.Add(Ui.Heading("최근 활동")); activity.Controls[0].Location = new Point(16, 14);
            lstActivity = MakeList(); lstActivity.SetBounds(12, 48, 300, 236); activity.Controls.Add(lstActivity);

            recentLogs = new CardPanel { Location = new Point(0, 300), Height = 300, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            recentLogs.Controls.Add(Ui.Heading("최근 로그")); recentLogs.Controls[0].Location = new Point(16, 14);
            more = Ui.Button("전체 보기", "", BtnStyle.Subtle); more.Location = new Point(300, 8);
            more.Click += delegate { Host.Navigate("logs"); };
            recentLogs.Controls.Add(more);
            lstLogs = MakeList(); lstLogs.SetBounds(12, 48, 300, 236); recentLogs.Controls.Add(lstLogs);

            Controls.AddRange(new Control[] { title, sub, grid, activity, recentLogs });
            Resize += delegate { Relayout(); };
            Relayout();

            _t.Tick += delegate { if (Visible) Refresh2(); };
            Host.Products.ListChanged += delegate { if (Visible) Refresh2(); };
            UsageMetricsService.Changed += delegate { try { if (Visible && IsHandleCreated) BeginInvoke((Action)Refresh2); } catch { } };
        }

        private FlatList MakeList()
        {
            var l = new FlatList { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(30), Font = Theme.FontBase() };
            l.DrawItem += DrawRow;
            return l;
        }

        private class Row { public string Time, Text; public Color? Tone; }

        private void DrawRow(object sender, DrawItemEventArgs e)
        {
            var l = (ListBox)sender; var p = Theme.Current;
            using (var b = new SolidBrush(p.Card)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            var r = (Row)l.Items[e.Index];
            TextRenderer.DrawText(e.Graphics, r.Time, Theme.Font(8.5f, FontStyle.Regular), new Rectangle(e.Bounds.X + Ui.S(6), e.Bounds.Y, Ui.S(78), e.Bounds.Height), p.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, r.Text, Theme.FontBase(), new Rectangle(e.Bounds.X + Ui.S(88), e.Bounds.Y, e.Bounds.Width - Ui.S(94), e.Bounds.Height), r.Tone ?? p.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            using (var pen = new Pen(p.Border)) e.Graphics.DrawLine(pen, e.Bounds.X + Ui.S(6), e.Bounds.Bottom - 1, e.Bounds.Right - Ui.S(6), e.Bounds.Bottom - 1);
        }

        private void Relayout()
        {
            int w = ClientSize.Width - Padding.Horizontal;
            if (w < Ui.S(300)) return;
            grid.Width = w + Ui.S(12);
            int gap = Ui.S(12); int half = (w - gap) / 2; int x0 = Padding.Left, y0 = Padding.Top + Ui.S(300), ch = Ui.S(300);
            activity.SetBounds(x0, y0, half, ch);
            recentLogs.SetBounds(x0 + half + gap, y0, w - half - gap, ch);
            more.Location = new Point(recentLogs.Width - more.Width - Ui.S(10), Ui.S(8));
            lstActivity.SetBounds(Ui.S(12), Ui.S(48), half - Ui.S(24), ch - Ui.S(60));
            lstLogs.SetBounds(Ui.S(12), Ui.S(48), recentLogs.Width - Ui.S(24), ch - Ui.S(60));
        }

        public override void OnActivated() { Refresh2(); _t.Start(); }
        public override void OnDeactivated() { _t.Stop(); }
        public override void OnThemeChanged() { activity.Invalidate(); recentLogs.Invalidate(); lstActivity.Invalidate(); lstLogs.Invalidate(); }

        public void Refresh2()
        {
            int done = Host.CountDone(), fail = Host.CountFail(), pend = Host.CountPending();
            cPending.Value = pend.ToString(); cPending.Hint = "아직 올리지 않은 유효한 상품";
            cDone.Value = done.ToString(); cDone.Hint = "등록 완료(상품번호가 있는 것 포함)";
            cFail.Value = fail.ToString(); cFail.Hint = fail > 0 ? "상태메시지를 확인하세요" : "실패한 상품이 없습니다";
            cTotal.Value = Host.Products.Count.ToString(); cTotal.Hint = Host.Products.Count == 0 ? "상품 관리에서 추가하거나 CSV로 불러오세요" : "목록에 있는 모든 상품";

            bool loggedIn = Host.Cfg.AssumeLoggedIn;
            cLogin.Value = loggedIn ? "확인됨" : "미확인"; cLogin.Hint = loggedIn ? "전용 크롬에 로그인되어 있음" : "로그인 메뉴에서 테스트하세요";
            cLogin.Tone = () => loggedIn ? Theme.Success : Theme.Warn;

            var li = LicenseGate.LastInfo;
            bool ok = li != null && li.Valid;
            cLic.Value = ok ? "정상" : "확인 필요"; cLic.Hint = ok ? (li.Name ?? "") : "라이선스 메뉴에서 확인하세요";
            cLic.Tone = () => ok ? Theme.Success : Theme.Danger;
            if (ok && li.Expires != DateTime.MaxValue)
            {
                cExp.Value = li.Expires.ToString("yyyy-MM-dd"); cExp.Hint = "이 날까지 사용할 수 있습니다";
                int days = (int)Math.Ceiling((li.Expires.Date.AddDays(1) - DateTime.Now).TotalDays);
                cLeft.Value = days > 0 ? days + "일" : "만료"; cLeft.Hint = days <= 7 ? "곧 만료됩니다. 연장을 요청하세요" : "남은 사용 기간";
                cLeft.Tone = () => days <= 7 ? Theme.Warn : Theme.Current.Text;
            }
            else if (ok) { cExp.Value = "무기한"; cExp.Hint = "기간 제한이 없는 라이선스"; cLeft.Value = "무기한"; cLeft.Hint = ""; cLeft.Tone = null; }
            else { cExp.Value = "-"; cExp.Hint = ""; cLeft.Value = "-"; cLeft.Hint = ""; }
            foreach (Control c in grid.Controls) c.Invalidate();

            // 최근 활동: 사용 기록 + 상품의 마지막 등록 시각
            var u = UsageMetricsService.Data; var rows = new List<Row>();
            if (!string.IsNullOrEmpty(u.LastRun)) rows.Add(new Row { Time = Short(u.LastRun), Text = "최근 자동등록 시작", Tone = null });
            if (!string.IsNullOrEmpty(u.LastSuccess)) rows.Add(new Row { Time = Short(u.LastSuccess), Text = "최근 성공 (누적 " + u.Success + "건)", Tone = Theme.Success });
            if (!string.IsNullOrEmpty(u.LastFail)) rows.Add(new Row { Time = Short(u.LastFail), Text = "최근 실패 (누적 " + u.Fail + "건)", Tone = Theme.Danger });
            if (!string.IsNullOrEmpty(u.LastError)) rows.Add(new Row { Time = Short(u.LastError), Text = "최근 오류 (누적 " + u.Errors + "건)", Tone = Theme.Danger });
            foreach (var p in Host.Products.Where(x => !string.IsNullOrEmpty(x.LastPosted)).OrderByDescending(x => x.LastPosted).Take(5))
                rows.Add(new Row { Time = Short(p.LastPosted), Text = "등록: " + p.Title, Tone = null });
            if (!string.IsNullOrEmpty(u.LastStart)) rows.Add(new Row { Time = Short(u.LastStart), Text = "프로그램 실행 (누적 " + u.Launches + "회)", Tone = Theme.TextMuted });
            if (rows.Count == 0) rows.Add(new Row { Time = "", Text = "아직 활동이 없습니다. 상품을 추가하고 자동 등록을 시작해 보세요.", Tone = Theme.TextMuted });
            lstActivity.BeginUpdate(); lstActivity.Items.Clear(); foreach (var r in rows) lstActivity.Items.Add(r); lstActivity.EndUpdate();

            var logs = Log.LoadRecent(8); logs.Reverse();
            lstLogs.BeginUpdate(); lstLogs.Items.Clear();
            foreach (var e in logs) lstLogs.Items.Add(new Row { Time = e.LocalTime.ToString("HH:mm:ss"), Text = e.Message.Replace("\n", " "), Tone = e.Level == "ERROR" || e.Level == "FATAL" ? Theme.Danger : e.Level == "WARN" ? Theme.Warn : (Color?)null });
            if (logs.Count == 0) lstLogs.Items.Add(new Row { Time = "", Text = "아직 기록된 로그가 없습니다.", Tone = Theme.TextMuted });
            lstLogs.EndUpdate();
        }

        private static string Short(string s) { DateTime d; return DateTime.TryParse(s, out d) ? (d.Date == DateTime.Today ? d.ToString("HH:mm:ss") : d.ToString("MM-dd HH:mm")) : s; }
    }
}
