using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>자동 등록 실행 화면. Runner / Bot / Schedule 은 그대로이고, AutomationController 의 상태를 보여 주기만 한다.</summary>
    public class AutomationPage : PageBase, IDpiLayout
    {
        private readonly FluentButton btnStart, btnPause, btnStop;
        private readonly Label lblState, lblCurrent, lblProgress, lblNext, lblMode, lblOk, lblFail, lblLeft;
        private readonly FluentProgress bar;
        private readonly Chip chipRun;
        private readonly Timer _t = new Timer { Interval = 500 };
        private readonly CardPanel cardMain, cardNums;
        private Label usage, clock,title,policy,contact; private int quotaTick; private bool ready,placing;

        public AutomationPage(AppHost host) : base(host)
        {
            Key = "automation"; PageTitle = "자동 등록";
            title = Ui.Title("등록 실행실"); title.Location = new Point(0, 0);


            cardMain = new CardPanel { Location = new Point(0, 74), Height = 250, Tinted=true };
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
            policy =Ui.Muted("기본 한도: 자정 초기화 · 등록 버튼 제출 시 1회 차감 · 제출 후 결과 미확인도 차감 유지"); policy.Location=new Point(2,530);
            contact =Ui.Muted("Pro 추가 등록권 문의  "+Brand.Email); contact.Location=new Point(2,560);
            Controls.AddRange(new Control[] { title, cardMain, cardNums,usage,clock,policy,contact });
            ready=true;Resize+=delegate{LayoutForDpi();};

            btnStart.Click += delegate { Host.Auto.Start(); };
            btnStop.Click += delegate { Host.Auto.Stop(); };
            btnPause.Click += delegate { if (Host.Auto.Paused) Host.Auto.Resume(); else Host.Auto.Pause(); };
            Host.Auto.Changed += delegate { if (IsHandleCreated) Sync(); };
            _t.Tick += delegate { if (Visible) Sync(); };
            Sync();
        }

        static int Wrap(Label label,int x,int y,int w,int h){label.AutoSize=false;label.SetBounds(x,y,w,Math.Max(h,TextRenderer.MeasureText(label.Text.Length==0?" ":label.Text,label.Font,new Size(w,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Height+Ui.S(4)));return label.Bottom;}
        public void LayoutForDpi(){
            if(!ready||placing)return;placing=true;SuspendLayout();
            try {
                Padding=new Padding(Ui.S(32),Ui.S(26),Ui.S(32),Ui.S(26));
                int x=Padding.Left+AutoScrollPosition.X,y=Padding.Top+AutoScrollPosition.Y;
                int w=Math.Max(Ui.S(300),ClientSize.Width-Padding.Horizontal),pad=Ui.S(20),gap=Ui.S(12);
                int row=Wrap(title,x,y,w,Ui.S(40));
                cardMain.SetBounds(x,row+Ui.S(20),w,Ui.S(250));
                lblState.Name="automationState";chipRun.Name="automationBadge";lblProgress.Name="automationProgress";
                chipRun.Location=new Point(w-pad-chipRun.Width,pad);
                row=Wrap(lblState,pad,pad,Math.Max(Ui.S(80),chipRun.Left-pad-gap),Ui.S(32));
                row=Wrap(lblCurrent,pad,Math.Max(row,chipRun.Bottom)+gap,w-pad*2,Ui.S(24));
                bar.Anchor=AnchorStyles.Top|AnchorStyles.Left;bar.SetBounds(pad,row+gap,w-pad*2,Ui.S(8));
                row=Wrap(lblProgress,pad,bar.Bottom+gap,w-pad*2,Ui.S(24));
                row=Wrap(lblNext,pad,row+Ui.S(6),w-pad*2,Ui.S(24));
                row=Wrap(lblMode,pad,row+Ui.S(6),w-pad*2,Ui.S(24))+gap;
                int bx=pad,bottom=row;
                foreach(var button in new[]{btnStart,btnPause,btnStop}){if(bx+button.Width>w-pad){bx=pad;row=bottom+gap;}button.Location=new Point(bx,row);bx=button.Right+gap;bottom=Math.Max(bottom,button.Bottom);}
                cardMain.Height=bottom+pad;
                cardNums.SetBounds(x,cardMain.Bottom+Ui.S(16),w,Ui.S(108));
                var labels=cardNums.Controls.OfType<Label>().ToArray();int col=(w-pad*2)/3;
                for(int i=0;i<3;i++){Wrap(labels[i*2],pad+i*col,Ui.S(14),col-Ui.S(8),Ui.S(24));Wrap(labels[i*2+1],pad+i*col,Ui.S(46),col-Ui.S(8),Ui.S(40));}
                row=Wrap(usage,x,cardNums.Bottom+Ui.S(20),w,Ui.S(28));
                row=Wrap(clock,x,row+gap,w,Ui.S(24));row=Wrap(policy,x,row+gap,w,Ui.S(24));row=Wrap(contact,x,row+gap,w,Ui.S(24));
                AutoScrollMinSize=new Size(0,row-AutoScrollPosition.Y+Padding.Bottom);
            }finally{ResumeLayout(false);placing=false;}
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

        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)_t.Stop();}
        protected override void Dispose(bool disposing){if(disposing)_t.Dispose();base.Dispose(disposing);}
        private void Sync()
        {
            if(clock!=null)clock.Text=ServerClock.Status; if(usage!=null && ++quotaTick%10==0)usage.Text=UsageQuota.Summary();
            var a = Host.Auto; var r = Host.Runner;
            bool running = r.Running;
            lblState.Text = a.Paused ? "일시정지" : running ? "실행 중" : a.Active ? "예약 대기" : a.StateText;
            btnStart.Enabled = !a.Active && !running;
            btnStop.Enabled = a.Active || running;
            btnPause.Enabled = a.Active || running;
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
            parts.Add(c.BatchRepeatEnabled ? (c.BatchRepeatClockAligned ? "전체 목록 · 매시 " + c.BatchRepeatMinuteOfHour.ToString("00") + "분" : "전체 목록 · " + c.BatchRepeatMinutes + "분 간격") : c.OneByOne ? (c.UseTimeRules ? "1개씩 · 시간대별 간격" : "1개씩 · " + c.OneByOneMinutes + "분 간격") : "한 번에 최대 30개");
            if (c.BatchRepeatEnabled && c.BatchRepeatWindowEnabled) parts.Add(BatchRepeatSchedule.WindowSummary(c)+(c.BatchRepeatImmediateOnStart?" · 시작 1회 즉시 (시간대 밖 포함)":""));
            if (!c.BatchRepeatEnabled && c.OneByOne && c.RepeatAll) parts.Add("총 " + c.RepeatCount + "회 반복");
            if (!c.BatchRepeatEnabled && c.AllowDuplicate) parts.Add("중복 등록 허용");
            lblMode.Text = "현재 설정: " + string.Join("  ·  ", parts.ToArray()) + (a.Active ? "   · 실행 중 설정 고정" : "   · 예약에서 변경");
            LayoutForDpi();
        }
    }
}
