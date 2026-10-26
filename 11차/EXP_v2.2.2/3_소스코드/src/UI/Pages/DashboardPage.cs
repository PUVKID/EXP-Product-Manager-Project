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
        private readonly HeroBanner hero;
        private readonly StatCard cPending, cDone, cFail, cTotal, cLogin, cLic, cExp, cLeft;
        private readonly CardPanel activity;
        private readonly FlatList lstActivity;
        private readonly Timer _t = new Timer { Interval = 2000 };
        private readonly TableLayoutPanel grid;


        public DashboardPage(AppHost host) : base(host)
        {
            Key = "dashboard"; PageTitle = "한눈에 보기";
            hero = new HeroBanner { Left=0,Top=0,Height=148,Width=940,Eyebrow="EXP  /  YOUR WORKSPACE",Title="오늘의 판매, 가볍게.",Subtitle="상품을 준비하고, 나머지는 EXP에 맡기세요.",ActionText="상품 준비하기  →",TabStop=true,Cursor=Cursors.Hand,AccessibleRole=AccessibleRole.PushButton,AccessibleName="상품 준비하기" };
            hero.Click += delegate { Host.Navigate("products"); };
            grid = new TableLayoutPanel { Location = new Point(0, 166), ColumnCount = 4, RowCount = 2, Height = 292, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = Theme.BgDark };
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Func<string, StatCard> mk = cap => { var c = new StatCard { Caption = cap, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12) }; grid.Controls.Add(c); return c; };
            cPending = mk("등록 대기"); cDone = mk("등록 성공"); cFail = mk("등록 실패"); cTotal = mk("전체 상품");
            cLogin = mk("현재 로그인 상태"); cLic = mk("라이선스 상태"); cExp = mk("라이선스 만료일"); cLeft = mk("남은 기간");
            cDone.Tone = () => Theme.Success; cFail.Tone = () => Theme.Danger;

            activity = new CardPanel { Location = new Point(0, 300), Height = 300, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            activity.Controls.Add(Ui.Heading("최근 활동")); activity.Controls[0].Location = new Point(16, 14);
            lstActivity = MakeList(); lstActivity.SetBounds(12, 48, 300, 236); activity.Controls.Add(lstActivity);

            Controls.AddRange(new Control[] { hero, grid, activity });
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
            hero.Width = w;
            grid.Width = w + Ui.S(12);
            int x0 = Padding.Left, y0 = Padding.Top + Ui.S(474), ch = Ui.S(244);
            activity.SetBounds(x0,y0,w,ch);
            lstActivity.SetBounds(Ui.S(16),Ui.S(52),w-Ui.S(32),ch-Ui.S(68));
        }

        public override void OnActivated() { Refresh2(); _t.Start(); }
        public override void OnDeactivated() { _t.Stop(); }
        public override void OnThemeChanged() { activity.Invalidate(); lstActivity.Invalidate(); }

        public void Refresh2()
        {
            int done = Host.CountDone(), fail = Host.CountFail(), pend = Host.CountPending();
            cPending.Value = pend.ToString(); cPending.Hint = "등록할 준비가 된 상품";
            cDone.Value = done.ToString(); cDone.Hint = "등록을 완료한 상품";
            cFail.Value = fail.ToString(); cFail.Hint = fail > 0 ? "상태메시지를 확인하세요" : "실패한 상품이 없습니다";
            cTotal.Value = Host.Products.Count.ToString(); cTotal.Hint = "저장한 상품 수";

            bool loggedIn = Host.Cfg.AssumeLoggedIn;
            cLogin.Value = loggedIn ? "확인됨" : "미확인"; cLogin.Hint = loggedIn ? "번개장터 로그인 확인" : "로그인 메뉴에서 테스트하세요";
            cLogin.Tone = () => loggedIn ? Theme.Success : Theme.Warn;

            var li = LicenseGate.LastInfo;
            bool ok = li != null && li.Valid;
            cLic.Value = ok ? li.Edition : "확인 필요"; cLic.Hint = ok ? (li.Trial ? "Pro 3시간 체험판" : "EXP 정식 라이선스") : "라이선스 메뉴에서 확인하세요";
            cLic.Tone = () => ok ? Theme.Success : Theme.Danger;
            if (ok && li.Expires != DateTime.MaxValue)
            {
                cExp.Value = li.Expires.ToString("yyyy-MM-dd"); cExp.Hint = li.Expires.ToString("HH:mm") + "까지 사용할 수 있습니다";
                int days = (int)Math.Ceiling((li.Expires.Date.AddDays(1) - DateTime.Now).TotalDays);
                cLeft.Value = days > 0 ? days + "일" : "만료"; cLeft.Hint = days <= 7 ? "곧 만료됩니다. 연장을 요청하세요" : "남은 사용 기간";
                var remaining=li.Expires.ToUniversalTime()-ServerClock.UtcNow;
                if(remaining.TotalHours<24) { cLeft.Value=Math.Max(0,(int)remaining.TotalMinutes)+"분"; cLeft.Hint=li.Trial?"체험판은 발급부터 3시간":"남은 사용 시간"; }
                cLeft.Tone = () => days <= 7 ? Theme.Warn : Theme.Current.Text;
            }
            else if (ok) { cExp.Value = "무기한"; cExp.Hint = "기간 제한 없음"; cLeft.Value = "무기한"; cLeft.Hint = ""; cLeft.Tone = null; }
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


        }

        private static string Short(string s) { DateTime d; return DateTime.TryParse(s, out d) ? (d.Date == DateTime.Today ? d.ToString("HH:mm:ss") : d.ToString("MM-dd HH:mm")) : s; }
    }
}
