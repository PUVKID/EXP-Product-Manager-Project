using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>현재 자동 등록 진행과 목록 집계를 표시합니다. 일일 사용량과 인증을 추가로 조회하지 않습니다.</summary>
    public class DashboardPage : PageBase, IDpiLayout
    {
        private readonly Label title, jobTitle, description, jobState, jobCurrent, jobNext, activityTitle;
        private readonly Label[] statCaptions, statValues, statusCaptions, statusValues;
        private readonly Label activityTime, activityText;
        private readonly CardPanel overview, activity;
        private readonly FlatList lstActivity;
        private readonly FluentButton openAutomation;
        private readonly RunFraction fraction;
        private readonly BrandScene progress;
        private readonly Timer refresh = new Timer { Interval = 2000 };
        private readonly Action usageChanged;
        private bool ready, placing;

        public DashboardPage(AppHost host) : base(host)
        {
            Key="dashboard";PageTitle="한눈에 보기";
            title=Ui.Title("판매 작업실");title.Font=Theme.Font(26f,FontStyle.Bold);title.Name="dashboardTitle";
            overview=new CardPanel {Name="dashboardOverview"};overview.Paint+=DrawOverviewLines;
            jobTitle=Ui.Text("현재 등록 진행",22f,true);jobTitle.Name="dashboardProgressTitle";
            description=Ui.Muted("작업 흐름을 확인하고 다음 등록을 준비하세요.");
            fraction=new RunFraction {Name="dashboardFraction"};
            progress=new BrandScene {Name="dashboardProgress"};
            jobState=Ui.Text("대기 중",11f,true);jobState.Name="dashboardState";
            jobCurrent=Ui.Muted("현재 상품: —");jobNext=Ui.Muted("다음 예정  —");
            statCaptions=new[]{Ui.Muted("등록 대기"),Ui.Muted("등록 성공"),Ui.Muted("등록 실패")};
            statValues=new[]{Ui.Text("0",24f,true,"t"),Ui.Text("0",24f,true,"s"),Ui.Text("0",24f,true,"d")};
            string[] statNames={"dashboardPending","dashboardDone","dashboardFail"};
            for(int i=0;i<3;i++){statValues[i].Name=statNames[i];statCaptions[i].TextAlign=ContentAlignment.MiddleCenter;statValues[i].TextAlign=ContentAlignment.MiddleCenter;overview.Controls.Add(statCaptions[i]);overview.Controls.Add(statValues[i]);}
            openAutomation=Ui.Button("자동 등록 열기  ›","\uE768",BtnStyle.Primary,180);openAutomation.Name="openAutomation";openAutomation.Height=Ui.S(44);
            openAutomation.Click+=delegate{Host.Navigate("automation");};
            statusCaptions=new[]{Ui.Muted("로그인"),Ui.Muted("프로그램"),Ui.Muted("남은 기간")};
            statusValues=new[]{Ui.Heading("미확인"),Ui.Heading("확인 필요"),Ui.Heading("—")};
            for(int i=0;i<3;i++){overview.Controls.Add(statusCaptions[i]);overview.Controls.Add(statusValues[i]);}
            overview.Controls.AddRange(new Control[]{jobTitle,description,fraction,progress,jobState,jobCurrent,jobNext,openAutomation});
            activityTitle=Ui.Heading("최근 활동");activityTitle.Font=Theme.Font(16f,FontStyle.Bold);
            activity=new CardPanel {Name="dashboardActivity"};activityTime=Ui.Muted("시간");activityText=Ui.Muted("작업 내용");
            lstActivity=new FlatList {Name="dashboardActivityList",DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Ui.S(34),Font=Theme.FontBase()};
            lstActivity.DrawItem+=DrawRow;activity.Controls.AddRange(new Control[]{activityTime,activityText,lstActivity});
            Controls.AddRange(new Control[]{title,overview,activityTitle,activity});
            ready=true;Resize+=delegate{LayoutForDpi();};refresh.Tick+=delegate{if(Visible)Refresh2();};
            Host.Products.ListChanged+=OnProductsChanged;
            usageChanged=delegate{try{if(Visible&&IsHandleCreated)BeginInvoke((Action)Refresh2);}catch(InvalidOperationException){}};
            UsageMetricsService.Changed+=usageChanged;LayoutForDpi();
        }
        private void OnProductsChanged(object sender,ListChangedEventArgs e){if(Visible)Refresh2();}
        private class Row {public string Time,Text;public Color? Tone;}
        private void DrawRow(object sender,DrawItemEventArgs e)
        {
            var p=Theme.Current;using(var b=new SolidBrush(p.Card))e.Graphics.FillRectangle(b,e.Bounds);
            if(e.Index<0)return;var row=(Row)lstActivity.Items[e.Index];var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics,row.Time,Theme.Font(9f,FontStyle.Bold),new Rectangle(e.Bounds.X+Ui.S(16),e.Bounds.Y,Ui.S(108),e.Bounds.Height),p.TextMuted,flags);
            TextRenderer.DrawText(e.Graphics,row.Text,Theme.FontBase(),new Rectangle(e.Bounds.X+Ui.S(132),e.Bounds.Y,Math.Max(1,e.Bounds.Width-Ui.S(148)),e.Bounds.Height),row.Tone??p.Text,flags);
            using(var pen=new Pen(p.Border))e.Graphics.DrawLine(pen,e.Bounds.Left+Ui.S(16),e.Bounds.Bottom-1,e.Bounds.Right-Ui.S(16),e.Bounds.Bottom-1);
        }
        private void DrawOverviewLines(object sender,PaintEventArgs e)
        {
            if(!ready)return;int pad=Ui.S(24),w=overview.Width;using(var pen=new Pen(Theme.Border)){
                e.Graphics.DrawLine(pen,pad,Ui.S(280),w-pad,Ui.S(280));
                e.Graphics.DrawLine(pen,0,Ui.S(432),w,Ui.S(432));
                int col=(w-pad*2)/3;for(int i=1;i<3;i++){int x=pad+i*col;e.Graphics.DrawLine(pen,x,Ui.S(296),x,Ui.S(352));e.Graphics.DrawLine(pen,x,Ui.S(450),x,Ui.S(488));}
            }
        }
        private static void TextBounds(Label label,int x,int y,int width,int height){label.AutoSize=false;label.AutoEllipsis=true;label.SetBounds(x,y,Math.Max(1,width),height);}
        public void LayoutForDpi()
        {
            if(!ready||placing)return;placing=true;SuspendLayout();
            try{
                Padding=new Padding(Ui.S(32),Ui.S(26),Ui.S(32),Ui.S(26));int x=Padding.Left+AutoScrollPosition.X,y=Padding.Top+AutoScrollPosition.Y;
                int w=Math.Max(Ui.S(420),ClientSize.Width-Padding.Horizontal),pad=Ui.S(24),ring=w>=Ui.S(650)?Ui.S(164):Ui.S(120),leftWidth=w-ring-pad*3;
                TextBounds(title,x,y,w,Ui.S(40));overview.SetBounds(x,title.Bottom+Ui.S(16),w,Ui.S(508));
                TextBounds(jobTitle,pad,Ui.S(20),leftWidth,Ui.S(38));TextBounds(description,pad,Ui.S(64),leftWidth,Ui.S(24));
                fraction.SetBounds(pad,Ui.S(92),leftWidth,Ui.S(92));progress.SetBounds(w-pad-ring,Ui.S(54),ring,ring);
                TextBounds(jobState,pad,Ui.S(190),leftWidth,Ui.S(26));TextBounds(jobCurrent,pad,Ui.S(220),w-pad*2,Ui.S(24));TextBounds(jobNext,pad,Ui.S(248),w-pad*2,Ui.S(24));
                int col=(w-pad*2)/3;
                for(int i=0;i<3;i++){
                    TextBounds(statCaptions[i],pad+i*col,Ui.S(292),col-Ui.S(8),Ui.S(24));TextBounds(statValues[i],pad+i*col,Ui.S(320),col-Ui.S(8),Ui.S(36));
                    TextBounds(statusCaptions[i],pad+i*col,Ui.S(442),col-Ui.S(8),Ui.S(22));TextBounds(statusValues[i],pad+i*col,Ui.S(470),col-Ui.S(8),Ui.S(24));
                }
                openAutomation.SetBounds(pad,Ui.S(372),w-pad*2,Ui.S(44));
                TextBounds(activityTitle,x,overview.Bottom+Ui.S(20),w,Ui.S(32));activity.SetBounds(x,activityTitle.Bottom+Ui.S(12),w,Ui.S(210));
                TextBounds(activityTime,Ui.S(16),Ui.S(8),Ui.S(108),Ui.S(26));TextBounds(activityText,Ui.S(132),Ui.S(8),w-Ui.S(148),Ui.S(26));
                lstActivity.SetBounds(1,Ui.S(40),w-2,activity.Height-Ui.S(48));AutoScrollMinSize=new Size(0,activity.Bottom-AutoScrollPosition.Y+Padding.Bottom);
            }finally{ResumeLayout(false);placing=false;}
        }
        public override void OnActivated(){Refresh2();refresh.Start();}
        public override void OnDeactivated(){refresh.Stop();}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)refresh.Stop();}
        public override void OnThemeChanged(){lstActivity.Invalidate();overview.Invalidate();}
        protected override void Dispose(bool disposing){if(disposing){refresh.Dispose();Host.Products.ListChanged-=OnProductsChanged;UsageMetricsService.Changed-=usageChanged;}base.Dispose(disposing);}
        public void Refresh2()
        {
            statValues[0].Text=Host.CountPending().ToString();statValues[1].Text=Host.CountDone().ToString();statValues[2].Text=Host.CountFail().ToString();
            var a=Host.Auto;jobState.Text=a.StateText;jobCurrent.Text="현재 상품: "+(Host.Runner.CurrentProduct==null?"—":Host.Runner.CurrentProduct.Title);
            fraction.SetProgress(a.Done,a.Total);progress.Maximum=Math.Max(1,a.Total);progress.Value=a.Total>0?a.Done:0;
            jobNext.Text=string.IsNullOrEmpty(a.NextText)?"다음 예정  —":a.NextText;
            statusValues[0].Text=Host.Cfg.AssumeLoggedIn?"확인됨":"미확인";var li=LicenseGate.LastInfo;bool ok=li!=null&&li.Valid;
            statusValues[1].Text=ok?"EXP "+li.Edition:"확인 필요";statusValues[2].Text=!ok?"—":li.Expires==DateTime.MaxValue?"무기한":Math.Max(0,(int)Math.Ceiling((li.Expires.ToUniversalTime()-ServerClock.UtcNow).TotalDays))+"일";
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
