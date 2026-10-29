using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>자동 등록 실행 화면. Runner / Bot / Schedule 은 그대로이고, AutomationController 의 상태를 보여 주기만 한다.</summary>
    public class AutomationPage : PageBase
    {
        private readonly FluentButton btnStart, btnPause, btnStop;
        private readonly Label lblState, lblCurrent, lblProgress, lblNext, lblMode, lblOk, lblFail, lblLeft;
        private readonly FluentProgress bar;
        private readonly Chip chipRun;
        private readonly Timer _t = new Timer { Interval = 500 };
        private readonly CardPanel cardMain, cardNums;
        private Label usage, clock; private int quotaTick;

        public AutomationPage(AppHost host) : base(host)
        {
            Key = "automation"; PageTitle = "자동 등록";
            var title = Ui.Title("자동 등록"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("상품 목록의 상품을 번개장터에 자동으로 올립니다."); sub.Location = new Point(2, 38);

            cardMain = new CardPanel { Location = new Point(0, 74), Height = 250 };
            lblState = Ui.Text("대기 중", 16f, true); lblState.Location = new Point(20, 18);
            chipRun = new Chip { Text = "중지됨", Tone = null }; chipRun.Location = new Point(300, 22);
            lblCurrent = Ui.Muted("현재 상품: -"); lblCurrent.Location = new Point(22, 58); lblCurrent.AutoSize = false; lblCurrent.Size = new Size(700, 22);
            bar = new FluentProgress { Location = new Point(22, 92), Height = Ui.S(8), Width = Ui.S(600) }; bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblProgress = Ui.Muted("0 / 0"); lblProgress.Location = new Point(22, 106);
            lblNext = Ui.Text("", 9.5f, false, "a"); lblNext.Location = new Point(22, 132);
            lblMode = Ui.Muted(""); lblMode.Location = new Point(22, 158); lblMode.AutoSize = false; lblMode.Size = new Size(700, 22);

            btnStart = Ui.Button("시작", "\uE768", BtnStyle.Primary, 120); btnStart.Location = new Point(22, 196);
            btnPause = Ui.Button("일시정지", "\uE769", BtnStyle.Standard, 130); btnPause.Location = new Point(152, 196);
            btnStop = Ui.Button("중지", "\uE71A", BtnStyle.Danger, 110); btnStop.Location = new Point(292, 196);
            cardMain.Controls.AddRange(new Control[] { lblState, chipRun, lblCurrent, bar, lblProgress, lblNext, lblMode, btnStart, btnPause, btnStop });

            cardNums = new CardPanel { Location = new Point(0, 338), Height = 100 };
            lblOk = NumBlock(cardNums, "이번 실행 성공", 20, "s");
            lblFail = NumBlock(cardNums, "이번 실행 실패", 220, "d");
            lblLeft = NumBlock(cardNums, "남은 상품", 420, "t");
            usage=Ui.Heading(UsageQuota.Summary()); usage.Location=new Point(2,462);
            clock=Ui.Muted(ServerClock.Status); clock.Location=new Point(2,498);
            var policy=Ui.Muted("기본 한도: 자정 초기화 · 등록 버튼 제출 시 1회 차감 · 제출 후 결과 미확인도 차감 유지"); policy.Location=new Point(2,530);
            var contact=Ui.Muted("Pro 추가 등록권 문의  "+Brand.Email); contact.Location=new Point(2,560);
            Controls.AddRange(new Control[] { title, sub, cardMain, cardNums,usage,clock,policy,contact });
            Resize += delegate { cardMain.Width = Math.Max(Ui.S(400), ClientSize.Width - Padding.Horizontal); cardNums.Width = cardMain.Width; bar.Width = cardMain.Width - Ui.S(44); };

            btnStart.Click += delegate { Host.Auto.Start(); };
            btnStop.Click += delegate { Host.Auto.Stop(); };
            btnPause.Click += delegate { if (Host.Auto.Paused) Host.Auto.Resume(); else Host.Auto.Pause(); };
            Host.Auto.Changed += delegate { if (IsHandleCreated) Sync(); };
            _t.Tick += delegate { if (Visible) Sync(); };
            Sync();
        }

        private static Label NumBlock(CardPanel card, string caption, int x, string tone)
        {
            var c = Ui.Muted(caption); c.Location = new Point(x, 14);
            var v = Ui.Text("0", 24f, true, tone); v.Location = new Point(x, 40);
            card.Controls.Add(c); card.Controls.Add(v);
            return v;
        }

        public override void OnActivated() { Sync(); _t.Start(); }
        public override void OnDeactivated() { _t.Stop(); }

        private void Sync()
        {
            if(clock!=null)clock.Text=ServerClock.Status; if(usage!=null && ++quotaTick%10==0)usage.Text=UsageQuota.Summary();
            var a = Host.Auto; var r = Host.Runner;
            bool running = r.Running;
            lblState.Text = a.StateText;
            btnStart.Enabled = !a.Active && !running;
            btnStop.Enabled = a.Active || running;
            btnPause.Enabled = running;
            btnPause.Text = a.Paused ? "계속" : "일시정지"; btnPause.Glyph = Theme.HasIconFont ? (a.Paused ? "\uE768" : "\uE769") : "";
            chipRun.Text = a.Paused ? "일시정지" : running ? "실행 중" : a.Active ? "예약 대기" : "중지됨";
            chipRun.Tone = a.Paused ? Theme.Warn : running ? Theme.Success : a.Active ? Theme.Accent : (Color?)null;
            chipRun.Invalidate();

            var cur = r.CurrentProduct;
            lblCurrent.Text = "현재 상품: " + (cur != null ? cur.Title : "-");
            bar.Maximum = Math.Max(1, a.Total); bar.Value = a.Total == 0 ? 0 : a.Done;
            lblProgress.Text = a.Total > 0 ? a.Done + " / " + a.Total + " 완료" + a.RoundText() : (a.Active ? "다음 실행을 기다리는 중" + a.RoundText() : "0 / 0");
            lblNext.Text = a.NextText;
            lblOk.Text = a.SuccessThisRun.ToString(); lblFail.Text = a.FailThisRun.ToString();
            lblLeft.Text = a.RemainingCount.ToString();

            var c = Host.Cfg; var parts = new System.Collections.Generic.List<string>();
            parts.Add(c.OneByOne ? (c.UseTimeRules ? "1개씩 · 시간대별 간격" : "1개씩 · " + c.OneByOneMinutes + "분 간격") : "한 번에 최대 30개");
            if (c.OneByOne && c.RepeatAll) parts.Add("총 " + c.RepeatCount + "회 반복");
            if (c.AllowDuplicate) parts.Add("중복 등록 허용");
            lblMode.Text = "현재 설정: " + string.Join("  ·  ", parts.ToArray()) + "   (예약 메뉴에서 바꿀 수 있습니다)";
        }
    }
}
