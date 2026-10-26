using System;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>등록 방식에 필요한 설정만 펼치며 실행 중에는 변경을 잠급니다.</summary>
    public class SchedulePage : PageBase, IDpiLayout
    {
        readonly Label title, sub, modeHint, batchHint, gapLabel, roundLabel;
        readonly CardPanel card;
        readonly Segmented mode, timing;
        readonly ToggleSwitch chkRegister, chkWindows, chkDup, chkRepeat;
        readonly SoftNumber numGap, numRounds, batchMinutes, batchMinute;
        readonly DataGridView gridRules;
        readonly FluentButton bAdd,bDel;
        readonly Action changed;
        bool loading,ready,placing;
        bool Advanced {get{var li=LicenseGate.LastInfo;return li!=null&&li.Valid&&Lumivolt.Security.EditionPolicy.CanRepeat(li.Edition);}}
        bool Locked {get{return Host.Auto.Active||Host.Runner.Running;}}
        public SchedulePage(AppHost host):base(host)
        {
            Key="schedule";PageTitle="예약";
            title=Ui.Title("등록 흐름 설정");sub=Ui.Muted("방식을 고르면 필요한 설정만 표시됩니다. 반복·시간대 예약은 Studio / Pro에서 사용할 수 있습니다.");
            card=new CardPanel{Name="scheduleCard"};
            chkRegister=Ui.Toggle("상품 등록 사용");chkRegister.Name="registerEnabled";
            mode=new Segmented{Name="registrationMode",AccessibleName="등록 방식"};mode.Items.AddRange(new[]{"한 번","1개씩","전체 목록 반복"});
            modeHint=Ui.Muted("");
            timing=new Segmented{Name="batchTiming",AccessibleName="전체 목록 반복 시각"};timing.Items.AddRange(new[]{"N분 간격","매시 지정분"});
            batchMinutes=Ui.Number(1,1440,144);batchMinutes.Name="batchMinutes";batchMinutes.AccessibleName="전체 목록 반복 간격 분";
            batchMinute=Ui.Number(0,59,144);batchMinute.Name="batchMinute";batchMinute.AccessibleName="매시 실행할 분";
            batchHint=Ui.Muted("");batchHint.Name="batchSummary";
            gapLabel=Ui.Muted("상품 사이 기본 간격(분)");numGap=Ui.Number(1,1440,144);numGap.Name="singleGap";
            chkWindows=Ui.Toggle("시간대별 간격 사용");chkWindows.Name="timeRulesEnabled";
            gridRules=new DataGridView{Name="timeRules",AutoGenerateColumns=false,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
            gridRules.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Start",HeaderText="시작 (HH:mm)"});gridRules.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="End",HeaderText="종료 (HH:mm)"});gridRules.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName="Minutes",HeaderText="간격(분)"});gridRules.DataSource=Host.Auto.Rules;
            bAdd=Ui.Button("시간대 추가");bDel=Ui.Button("선택 삭제");
            chkRepeat=Ui.Toggle("목록 완료 후 처음부터 반복");chkRepeat.Name="legacyRepeat";
            roundLabel=Ui.Muted("총 횟수 (처음 1회 포함)");numRounds=Ui.Number(2,999,144);numRounds.Name="legacyRounds";
            chkDup=Ui.Toggle("이미 등록된 상품도 포함");chkDup.Name="allowDuplicate";
            card.Controls.AddRange(new Control[]{chkRegister,mode,modeHint,timing,batchMinutes,batchMinute,batchHint,gapLabel,numGap,chkWindows,gridRules,bAdd,bDel,chkRepeat,roundLabel,numRounds,chkDup});Controls.AddRange(new Control[]{title,sub,card});
            foreach(var t in new[]{chkRegister,chkWindows,chkDup,chkRepeat})t.CheckedChanged+=Save;
            foreach(var n in new[]{numGap,numRounds,batchMinutes,batchMinute})n.ValueChanged+=Save;
            mode.SelectedIndexChanged+=delegate{if(loading)return;if(Locked||(mode.SelectedIndex==2&&!Advanced)){LoadSettings();return;}Save(null,EventArgs.Empty);};timing.SelectedIndexChanged+=Save;
            gridRules.CellEndEdit+=delegate{if(!Locked)Host.SaveSettings();};
            bAdd.Click+=delegate{if(!Locked&&Advanced){Host.Auto.Rules.Add(new TimeRule("00:00","24:00",60));Host.SaveSettings();}};
            bDel.Click+=delegate{if(!Locked&&Advanced&&gridRules.CurrentRow!=null&&gridRules.CurrentRow.Index>=0&&gridRules.CurrentRow.Index<Host.Auto.Rules.Count){Host.Auto.Rules.RemoveAt(gridRules.CurrentRow.Index);Host.SaveSettings();}};
            changed=delegate{if(!IsDisposed)UpdateState();};Host.Auto.Changed+=changed;
            ready=true;Resize+=delegate{LayoutForDpi();};LoadSettings();Theme.StyleGrid(gridRules);
        }
        static decimal Clamp(int value,SoftNumber n){return Math.Max(n.Minimum,Math.Min(n.Maximum,value));}
        void LoadSettings(){loading=true;var c=Host.Cfg;chkRegister.Checked=c.DoRegister;mode.SetIndexSilently(c.BatchRepeatEnabled?2:c.OneByOne?1:0);timing.SetIndexSilently(c.BatchRepeatClockAligned?1:0);batchMinutes.Value=Clamp(c.BatchRepeatMinutes,batchMinutes);batchMinute.Value=Clamp(c.BatchRepeatMinuteOfHour,batchMinute);numGap.Value=Clamp(c.OneByOneMinutes,numGap);numRounds.Value=Clamp(c.RepeatCount,numRounds);chkWindows.Checked=c.UseTimeRules;chkDup.Checked=c.AllowDuplicate;chkRepeat.Checked=c.RepeatAll;loading=false;UpdateState();}
        void Save(object sender,EventArgs e){if(loading)return;if(Locked){LoadSettings();return;}var c=Host.Cfg;c.DoRegister=chkRegister.Checked;c.OneByOne=mode.SelectedIndex==1;c.BatchRepeatEnabled=mode.SelectedIndex==2;c.OneByOneMinutes=(int)numGap.Value;if(Advanced){c.BatchRepeatClockAligned=timing.SelectedIndex==1;c.BatchRepeatMinutes=(int)batchMinutes.Value;c.BatchRepeatMinuteOfHour=(int)batchMinute.Value;c.UseTimeRules=chkWindows.Checked;c.AllowDuplicate=chkDup.Checked;c.RepeatAll=chkRepeat.Checked;c.RepeatCount=(int)numRounds.Value;}Host.SaveSettings();UpdateState();}
        void UpdateState(){if(!ready)return;bool one=mode.SelectedIndex==1,batch=mode.SelectedIndex==2,edit=!Locked;chkRegister.Enabled=mode.Enabled=edit;timing.Enabled=batchMinutes.Enabled=batchMinute.Enabled=edit&&Advanced;chkWindows.Enabled=chkDup.Enabled=chkRepeat.Enabled=edit&&Advanced;numGap.Enabled=edit&&!chkWindows.Checked;numRounds.Enabled=edit&&Advanced;gridRules.Enabled=bAdd.Enabled=bDel.Enabled=edit&&Advanced;modeHint.Text=Locked?"실행 중에는 예약을 변경할 수 없습니다. 중지한 뒤 설정해 주세요.":batch?"전체 유효 목록을 즉시 등록하고, 이후 정해진 시각에 다시 등록합니다.":one?"상품을 하나씩 등록합니다. 시간대 간격과 횟수 반복을 추가할 수 있습니다.":"상품을 한 번 등록합니다. 한 번에 최대 30개를 처리합니다.";batchHint.Text=timing.SelectedIndex==0?batchMinutes.Value+"분 간격으로 전체 목록 등록 · 1~1440분\n예: 15:02 시작 + 60분 → 16:02. 늦어진 회차는 몰아 실행하지 않습니다.":"매시 "+batchMinute.Value.ToString("00")+"분에 전체 목록 등록 · 0~59분\n예: 15:02 시작 + 매시 00분 → 16:00. 첫 회차는 즉시 시작합니다.";timing.Visible=batch;batchMinutes.Visible=batch&&timing.SelectedIndex==0;batchMinute.Visible=batch&&timing.SelectedIndex==1;batchHint.Visible=batch;gapLabel.Visible=numGap.Visible=one&&!chkWindows.Checked;chkWindows.Visible=chkRepeat.Visible=one;gridRules.Visible=bAdd.Visible=bDel.Visible=one&&chkWindows.Checked;roundLabel.Visible=numRounds.Visible=one&&chkRepeat.Checked;chkDup.Visible=!batch;LayoutForDpi();}
        int Put(Control c,int x,int y,int w,int h){c.SetBounds(x,y,w,h);return c.Bottom+Ui.S(14);}
        int LabelAt(Label l,int x,int y,int w){l.AutoSize=false;return Put(l,x,y,w,Math.Max(Ui.S(26),TextRenderer.MeasureText(l.Text,l.Font,new Size(w,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Height+Ui.S(6)));}
        public void LayoutForDpi(){if(!ready||placing)return;placing=true;try{Padding=new Padding(Ui.S(32),Ui.S(26),Ui.S(32),Ui.S(26));int x=Padding.Left+AutoScrollPosition.X,y=Padding.Top+AutoScrollPosition.Y,w=Math.Max(Ui.S(340),ClientSize.Width-Padding.Horizontal),p=Ui.S(24),inner=w-p*2;y=LabelAt(title,x,y,w);y=LabelAt(sub,x,y,w);card.SetBounds(x,y,w,Ui.S(600));int row=p;row=Put(chkRegister,p,row,inner,Ui.S(28));row=Put(mode,p,row,inner,Ui.S(42));row=LabelAt(modeHint,p,row,inner);
            if(timing.Visible){row=Put(timing,p,row,inner,Ui.S(38));row=Put(timing.SelectedIndex==0?batchMinutes:batchMinute,p,row,Ui.S(160),Ui.S(40));row=LabelAt(batchHint,p,row,inner);}
            if(chkWindows.Visible){row=Put(chkWindows,p,row,inner,Ui.S(28));if(numGap.Visible){row=LabelAt(gapLabel,p,row,inner);row=Put(numGap,p,row,Ui.S(160),Ui.S(40));}if(gridRules.Visible){row=Put(gridRules,p,row,inner,Ui.S(144));bAdd.SetBounds(p,row,Ui.S(130),Ui.S(36));bDel.SetBounds(p+Ui.S(142),row,Ui.S(110),Ui.S(36));row=bAdd.Bottom+Ui.S(18);}row=Put(chkRepeat,p,row,inner,Ui.S(28));if(numRounds.Visible){row=LabelAt(roundLabel,p,row,inner);row=Put(numRounds,p,row,Ui.S(160),Ui.S(40));}}
            if(chkDup.Visible)row=Put(chkDup,p,row,inner,Ui.S(28));card.Height=row+p;AutoScrollMinSize=new Size(0,card.Bottom-AutoScrollPosition.Y+Padding.Bottom);
        }finally{placing=false;}}
        public override void OnActivated(){LoadSettings();}
        public override void OnThemeChanged(){Theme.StyleGrid(gridRules);UpdateState();}
        protected override void Dispose(bool disposing){if(disposing)Host.Auto.Changed-=changed;base.Dispose(disposing);}
    }
}
