using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>상품의 실제 집계와 현재 자동 등록 상태를 읽는 대시보드입니다. 인증·일일 사용량을 새로 조회하지 않습니다.</summary>
    public class DashboardPage : PageBase, IDpiLayout
    {
        private readonly Label title, subtitle, jobTitle, jobState, jobCurrent, jobNext, progressText, activityTitle;
        private readonly Label[] statusCaptions, statusValues;
        private readonly StatCard cPending, cDone, cFail, cTotal;
        private readonly CardPanel job, status, activity;
        private readonly FlatList lstActivity;
        private readonly FluentButton openAutomation;
        private readonly FluentProgress progress;
        private readonly Timer refresh = new Timer { Interval = 2000 };
        private readonly TableLayoutPanel grid;
        private readonly Action usageChanged;
        private bool ready, placing;

        public DashboardPage(AppHost host) : base(host)
        {
            Key = "dashboard"; PageTitle = "한눈에 보기";
            title = Ui.Title("한눈에 보기"); title.Font=Theme.Font(28f,FontStyle.Bold); title.Name = "dashboardTitle";
            subtitle = Ui.Muted("등록 현황과 최근 활동을 한눈에 확인하세요.");
            grid = new TableLayoutPanel { Name="dashboardStats", ColumnCount=4, RowCount=1, BackColor=Theme.BgDark, Margin=Padding.Empty, Padding=Padding.Empty };
            for(int i=0;i<4;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Func<string, StatCard> make = caption => {var card=new StatCard {Caption=caption,Dock=DockStyle.Fill,Margin=new Padding(0,0,Ui.S(12),0)};grid.Controls.Add(card);return card;};
            cPending=make("등록 대기"); cDone=make("등록 성공"); cFail=make("등록 실패"); cTotal=make("전체 상품");
            cDone.Tone=()=>Theme.TextPrimary; cFail.Tone=()=>Theme.Danger;
            job=new CardPanel {Name="dashboardAutomation",Tinted=true};
            jobTitle=Ui.Heading("자동 등록");jobTitle.Font=Theme.Font(18f,FontStyle.Bold);
            jobState=Ui.Text("대기 중",18f,true);jobState.Name="dashboardState";
            jobCurrent=Ui.Muted("현재 상품: -");jobNext=Ui.Muted("다음 예정  —");progressText=Ui.Muted("0 / 0");
            progress=new FluentProgress {Name="dashboardProgress"};
            openAutomation=Ui.Button("자동 등록 열기", "\uE768", BtnStyle.Primary,180);
            openAutomation.Name="openAutomation";openAutomation.Click+=delegate{Host.Navigate("automation");};
            job.Controls.AddRange(new Control[]{jobTitle,jobState,jobCurrent,progress,progressText,jobNext,openAutomation});
            status=new CardPanel {Name="dashboardStatus"};
            statusCaptions=new[]{Ui.Muted("로그인"),Ui.Muted("프로그램"),Ui.Muted("남은 기간")};
            statusValues=new[]{Ui.Heading("미확인"),Ui.Heading("확인 필요"),Ui.Heading("—")};
            for(int i=0;i<3;i++){status.Controls.Add(statusCaptions[i]);status.Controls.Add(statusValues[i]);}
            activityTitle=Ui.Heading("최근 활동");activityTitle.Font=Theme.Font(16f,FontStyle.Bold);
            activity=new CardPanel {Name="dashboardActivity"};
            lstActivity=new FlatList {Name="dashboardActivityList",DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Ui.S(40),Font=Theme.FontBase()};
            lstActivity.DrawItem+=DrawRow;activity.Controls.Add(lstActivity);
            Controls.AddRange(new Control[]{title,subtitle,grid,job,status,activityTitle,activity});
            ready=true;Resize+=delegate{LayoutForDpi();};
            refresh.Tick+=delegate{if(Visible)Refresh2();};
            Host.Products.ListChanged+=OnProductsChanged;
            usageChanged=delegate{try{if(Visible&&IsHandleCreated)BeginInvoke((Action)Refresh2);}catch(InvalidOperationException){}};
            UsageMetricsService.Changed+=usageChanged;
            LayoutForDpi();
        }

        private void OnProductsChanged(object sender,ListChangedEventArgs e){if(Visible)Refresh2();}
        private class Row {public string Time,Text;public Color? Tone;}
        private void DrawRow(object sender,DrawItemEventArgs e)
        {
            var p=Theme.Current;using(var b=new SolidBrush(p.Card))e.Graphics.FillRectangle(b,e.Bounds);
            if(e.Index<0)return;var row=(Row)lstActivity.Items[e.Index];
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics,row.Time,Theme.Font(9f,FontStyle.Bold),new Rectangle(e.Bounds.X+Ui.S(16),e.Bounds.Y,Ui.S(108),e.Bounds.Height),p.TextMuted,flags);
            TextRenderer.DrawText(e.Graphics,row.Text,Theme.FontBase(),new Rectangle(e.Bounds.X+Ui.S(132),e.Bounds.Y,Math.Max(1,e.Bounds.Width-Ui.S(148)),e.Bounds.Height),row.Tone??p.Text,flags);
            using(var pen=new Pen(p.Border))e.Graphics.DrawLine(pen,e.Bounds.Left+Ui.S(16),e.Bounds.Bottom-1,e.Bounds.Right-Ui.S(16),e.Bounds.Bottom-1);
        }
        private static void TextBounds(Label label,int x,int y,int width,int height){label.AutoSize=false;label.AutoEllipsis=true;label.SetBounds(x,y,Math.Max(1,width),height);}
        public void LayoutForDpi()
        {
            if(!ready||placing)return;placing=true;SuspendLayout();
            try{
                Padding=new Padding(Ui.S(32),Ui.S(26),Ui.S(32),Ui.S(26));
                int x=Padding.Left+AutoScrollPosition.X,y=Padding.Top+AutoScrollPosition.Y;
                int w=Math.Max(Ui.S(420),ClientSize.Width-Padding.Horizontal),pad=Ui.S(24),gap=Ui.S(16);
                TextBounds(title,x,y,w,Ui.S(44));TextBounds(subtitle,x,title.Bottom+Ui.S(5),w,Ui.S(28));
                grid.SetBounds(x,subtitle.Bottom+Ui.S(18),w+Ui.S(12),Ui.S(112));
                job.SetBounds(x,grid.Bottom+gap,w,Ui.S(218));
                TextBounds(jobTitle,pad,Ui.S(22),w-pad*2,Ui.S(32));
                bool wide=w>=Ui.S(660);int contentWidth=wide?w-pad*3-openAutomation.Width:w-pad*2;
                TextBounds(jobState,pad,Ui.S(66),contentWidth,Ui.S(34));
                TextBounds(jobCurrent,pad,Ui.S(106),contentWidth,Ui.S(24));
                progress.SetBounds(pad,Ui.S(142),contentWidth,Ui.S(8));
                TextBounds(progressText,pad,Ui.S(158),contentWidth,Ui.S(24));
                TextBounds(jobNext,pad,Ui.S(186),w-pad*2,Ui.S(24));
                openAutomation.Location=wide?new Point(w-pad-openAutomation.Width,Ui.S(108)):new Point(pad,Ui.S(232));
                job.Height=wide?Ui.S(218):openAutomation.Bottom+pad;
                status.SetBounds(x,job.Bottom+gap,w,Ui.S(80));
                int col=(w-pad*2)/3;
                for(int i=0;i<3;i++){TextBounds(statusCaptions[i],pad+i*col,Ui.S(16),col-Ui.S(12),Ui.S(24));TextBounds(statusValues[i],pad+i*col,Ui.S(44),col-Ui.S(12),Ui.S(26));}
                TextBounds(activityTitle,x,status.Bottom+Ui.S(20),w,Ui.S(32));
                activity.SetBounds(x,activityTitle.Bottom+Ui.S(12),w,Ui.S(164));
                lstActivity.SetBounds(1,Ui.S(8),w-2,activity.Height-Ui.S(16));
                AutoScrollMinSize=new Size(0,activity.Bottom-AutoScrollPosition.Y+Padding.Bottom);
            }finally{ResumeLayout(false);placing=false;}
        }
        public override void OnActivated(){Refresh2();refresh.Start();}
        public override void OnDeactivated(){refresh.Stop();}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)refresh.Stop();}
        public override void OnThemeChanged(){grid.BackColor=Theme.BgDark;lstActivity.Invalidate();}
        protected override void Dispose(bool disposing)
        {
            if(disposing){refresh.Dispose();Host.Products.ListChanged-=OnProductsChanged;UsageMetricsService.Changed-=usageChanged;}
            base.Dispose(disposing);
        }
        public void Refresh2()
        {
            cPending.Value=Host.CountPending().ToString();cDone.Value=Host.CountDone().ToString();cFail.Value=Host.CountFail().ToString();cTotal.Value=Host.Products.Count.ToString();
            cPending.Hint="준비된 상품";cDone.Hint="목록의 등록 완료";cFail.Hint="상태 확인 필요";cTotal.Hint="저장한 상품";
            foreach(Control c in grid.Controls)c.Invalidate();
            var a=Host.Auto;jobState.Text=a.StateText;jobCurrent.Text="현재 상품: "+(Host.Runner.CurrentProduct==null?"—":Host.Runner.CurrentProduct.Title);
            progress.Maximum=Math.Max(1,a.Total);progress.Value=a.Total>0?a.Done:0;
            progressText.Text=a.Total>0?a.Done+" / "+a.Total+" 완료"+a.RoundText():"등록을 시작하면 진행 상황이 표시됩니다.";
            jobNext.Text=string.IsNullOrEmpty(a.NextText)?"다음 예정  —":a.NextText;
            statusValues[0].Text=Host.Cfg.AssumeLoggedIn?"확인됨":"미확인";
            var li=LicenseGate.LastInfo;bool ok=li!=null&&li.Valid;
            statusValues[1].Text=ok?"EXP "+li.Edition:"확인 필요";
            statusValues[2].Text=!ok?"—":li.Expires==DateTime.MaxValue?"무기한":Math.Max(0,(int)Math.Ceiling((li.Expires.ToUniversalTime()-ServerClock.UtcNow).TotalDays))+"일";
            var u=UsageMetricsService.Data;var rows=new List<Row>();
            if(!string.IsNullOrEmpty(u.LastRun))rows.Add(new Row{Time=Short(u.LastRun),Text="최근 자동 등록 시작"});
            if(!string.IsNullOrEmpty(u.LastSuccess))rows.Add(new Row{Time=Short(u.LastSuccess),Text="최근 성공 · 누적 "+u.Success+"건",Tone=Theme.Success});
            if(!string.IsNullOrEmpty(u.LastFail))rows.Add(new Row{Time=Short(u.LastFail),Text="최근 실패 · 누적 "+u.Fail+"건",Tone=Theme.Danger});
            if(!string.IsNullOrEmpty(u.LastError))rows.Add(new Row{Time=Short(u.LastError),Text="최근 오류 · 누적 "+u.Errors+"건",Tone=Theme.Danger});
            foreach(var p in Host.Products.Where(v=>!string.IsNullOrEmpty(v.LastPosted)).OrderByDescending(v=>v.LastPosted).Take(5))rows.Add(new Row{Time=Short(p.LastPosted),Text="등록: "+p.Title});
            if(rows.Count==0)rows.Add(new Row{Time="—",Text="아직 활동이 없습니다. 상품을 준비하고 자동 등록을 시작하세요.",Tone=Theme.TextMuted});
            lstActivity.BeginUpdate();lstActivity.Items.Clear();foreach(var row in rows)lstActivity.Items.Add(row);lstActivity.EndUpdate();
        }
        private static string Short(string s){DateTime d;return DateTime.TryParse(s,out d)?d.ToString(d.Date==DateTime.Today?"HH:mm:ss":"MM-dd HH:mm"):s;}
    }
}
