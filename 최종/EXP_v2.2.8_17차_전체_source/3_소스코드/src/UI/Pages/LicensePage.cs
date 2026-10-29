using System;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>라이선스 센터: 상태·이름·만료일·남은 기간·PC 고유번호(복사)·키 교체. 검증은 기존 License.Verify 를 그대로 쓴다.</summary>
    public class LicensePage : PageBase, IDpiLayout
    {
        private readonly Label lblStatus, lblName, lblExp, lblLeft, lblPc, lblMsg;
        private readonly SoftInput txtKey;
        private readonly Chip chip;
        private CardPanel c1,c2,extra;
        private Label title,sub,h1,h2,d2,h3,info,contact;
        private SoftInput credit;
        private FluentButton bCopy,bRequest,bOk,bImport,redeem;
        private readonly System.Collections.Generic.List<Label> captions=new System.Collections.Generic.List<Label>();
        private readonly System.Collections.Generic.List<Label> values=new System.Collections.Generic.List<Label>();
        private bool ready,placing;

        public LicensePage(AppHost host) : base(host)
        {
            Key = "license"; PageTitle = "라이선스";
            title = Ui.Title("라이선스"); title.Location = new Point(0, 0);
            sub = Ui.Muted("이 PC 한 대에서 기간 동안만 사용할 수 있는 라이선스입니다."); sub.Location = new Point(2, 38);

            c1 = new CardPanel { Location = new Point(0, 74), Height = 230, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            h1 = Ui.Heading("라이선스 정보"); h1.Location = new Point(20, 16);
            chip = new Chip { Text = "확인 중" }; chip.Location = new Point(160, 18);
            int y = 56; Func<string, Label> row = cap =>
            {
                var a = Ui.Muted(cap); a.Location = new Point(22, y);
                var b = Ui.Text("-", 10f, true); b.Location = new Point(150, y - 2);
                c1.Controls.Add(a); c1.Controls.Add(b); captions.Add(a); values.Add(b); y += 32; return b;
            };
            lblName = row("라이선스 이름"); lblExp = row("만료일"); lblLeft = row("남은 기간");
            lblPc = row("PC 고유번호"); lblPc.Font = Theme.FontMono(11f);
            bCopy = Ui.Button("등록코드 복사", "\uE8C8", BtnStyle.Primary, 150); bCopy.Location = new Point(330, 177);
            lblStatus = lblName; // (호환용)
            bRequest = Ui.Button("파일로 저장", "", BtnStyle.Standard, 150); bRequest.Location = new Point(490, 177);
            bRequest.Click += delegate { LicenseFiles.ExportRequest(this); Sync(); };
            c1.Controls.AddRange(new Control[] { h1, chip, bCopy, bRequest });

            c2 = new CardPanel { Location = new Point(0, 316), Height = 210, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            h2 = Ui.Heading("라이선스 키 변경 / 연장"); h2.Location = new Point(20, 16);
            d2 = Ui.Muted("관리자에게 받은 새 키를 붙여 넣고 [인증]을 누르세요. 기간이 연장된 키도 같은 방법입니다."); d2.Location = new Point(22, 46);
            txtKey = new SoftInput { Location = new Point(22, 76), Width = 560, Height = 64, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Theme.FontMono(9f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            bOk = Ui.Button("인증", "\uE73E", BtnStyle.Primary, 110); bOk.Location = new Point(22, 152);
            lblMsg = Ui.Text("", 9.5f, false, "m"); lblMsg.Location = new Point(146, 160);
            bImport = Ui.Button("키 붙여넣기", "", BtnStyle.Standard, 120); bImport.Location = new Point(146, 152);
            lblMsg.Location = new Point(272, 160);
            bImport.Click += delegate { try { txtKey.Text = LicenseFiles.PasteKey(); } catch (Exception ex) { lblMsg.Text = ex is FormatException ? ex.Message : "직접 붙여 넣어 주세요."; } };
            c2.Controls.AddRange(new Control[] { h2, d2, txtKey, bOk, bImport, lblMsg });

            extra =new CardPanel {Left=0,Top=542,Height=178};
            h3 =Ui.Heading("Pro 추가 등록권"); h3.Location=new Point(20,16);
            info =Ui.Muted("기본 한도를 다 쓰면 추가 등록권을 사용합니다. 미사용 잔액은 이월됩니다.");info.Location=new Point(22,48);
            credit =Design.Input("creditKey",22,78,470,44,false);
            redeem =Ui.Button("충전", "",BtnStyle.Primary,110);redeem.Location=new Point(510,80);
            contact =Ui.Muted("추가 구매 문의  "+Brand.Email); contact.Location=new Point(22,138);
            redeem.Click+=delegate { try { UsageQuota.Redeem(credit.Text);credit.Clear();info.Text=UsageQuota.Summary(); }catch(Exception ex){info.Text=ex.Message;} };
            extra.Controls.AddRange(new Control[]{h3,info,credit,redeem,contact});
            Controls.AddRange(new Control[] { title, sub, c1, c2,extra });

            c1.Name="licenseInfoCard";c2.Name="licenseKeyCard";extra.Name="licenseCreditCard";
            txtKey.Name="licenseKeyInput";bOk.Name="activateLicense";bImport.Name="pasteLicense";
            credit.Name="creditInput";redeem.Name="redeemCredit";bCopy.Name="copyRegistration";bRequest.Name="saveRegistration";
            ready=true;Resize+=delegate{LayoutForDpi();};
            lblMsg.TextChanged+=delegate{LayoutForDpi();};info.TextChanged+=delegate{LayoutForDpi();};
            foreach(var value in values)value.TextChanged+=delegate{LayoutForDpi();};
            LayoutForDpi();
            bCopy.Click += delegate { string token, msg; bool ok = LicenseFiles.CopyRequest(out token, out msg); lblMsg.Text = msg; lblMsg.Tag = ok ? "s" : "d"; lblMsg.ForeColor = ok ? Theme.Success : Theme.Danger; if (!ok && token.Length > 0) { txtKey.Text = token; txtKey.SelectAll(); } Sync(); };
            bOk.Click += delegate
            {
                string msg;
                bool ok = LicenseGate.TryActivate(txtKey.Text, out msg);
                lblMsg.Text = msg; lblMsg.Tag = ok ? "s" : "d"; lblMsg.ForeColor = ok ? Theme.Success : Theme.Danger;
                if (ok) { txtKey.Text = ""; Sync(); }
            };
        }

        private static int PlaceText(Label label,int x,int y,int width,int minimum)
        {
            label.AutoSize=false;
            int h=Math.Max(minimum,TextRenderer.MeasureText(label.Text.Length==0?" ":label.Text,label.Font,
                new Size(Math.Max(1,width),int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Height+Ui.S(4));
            label.SetBounds(x,y,Math.Max(1,width),h);return y+h;
        }
        public void LayoutForDpi()
        {
            if(!ready||placing)return;
            placing=true;SuspendLayout();
            try {
                Padding=new Padding(Ui.S(32),Ui.S(26),Ui.S(32),Ui.S(26));
                int w=Math.Max(Ui.S(260),ClientSize.Width-Padding.Horizontal),pad=Ui.S(20),gap=Ui.S(12);
                int x=Padding.Left+AutoScrollPosition.X,y=Padding.Top+AutoScrollPosition.Y;
                int end=PlaceText(title,x,y,w,Ui.S(36));
                end=PlaceText(sub,x,end+Ui.S(6),w,Ui.S(24))+Ui.S(20);
                foreach(var card in new[]{c1,c2,extra}){card.Anchor=AnchorStyles.Top|AnchorStyles.Left;card.Padding=new Padding(pad);card.Width=w;}
                c1.Location=new Point(x,end);
                int row=PlaceText(h1,pad,pad,w-Ui.S(150),Ui.S(28));
                chip.Location=new Point(w-pad-chip.Width,pad);
                row=Math.Max(row,chip.Bottom)+gap;
                for(int i=0;i<values.Count;i++){
                    if(w<Ui.S(420)){
                        row=PlaceText(captions[i],pad,row,w-pad*2,Ui.S(22));
                        row=PlaceText(values[i],pad,row,w-pad*2,Ui.S(26))+gap;
                    } else {
                        int a=PlaceText(captions[i],pad,row,Ui.S(116),Ui.S(28));
                        int b=PlaceText(values[i],Ui.S(150),row,w-Ui.S(150)-pad,Ui.S(28));
                        row=Math.Max(a,b)+Ui.S(8);
                    }
                }
                bCopy.Location=new Point(pad,row+Ui.S(4));
                bRequest.Location=w>=bCopy.Width+bRequest.Width+pad*2+gap
                    ?new Point(bCopy.Right+gap,bCopy.Top):new Point(pad,bCopy.Bottom+gap);
                c1.Height=Math.Max(bCopy.Bottom,bRequest.Bottom)+pad;

                c2.Location=new Point(x,c1.Bottom+Ui.S(16));
                row=PlaceText(h2,pad,pad,w-pad*2,Ui.S(28));
                row=PlaceText(d2,pad,row+Ui.S(6),w-pad*2,Ui.S(24));
                txtKey.Anchor=AnchorStyles.Top|AnchorStyles.Left;
                txtKey.SetBounds(pad,row+gap,w-pad*2,Ui.S(88));
                bOk.Location=new Point(pad,txtKey.Bottom+gap);
                bImport.Location=w>=bOk.Width+bImport.Width+pad*2+gap
                    ?new Point(bOk.Right+gap,bOk.Top):new Point(pad,bOk.Bottom+gap);
                row=Math.Max(bOk.Bottom,bImport.Bottom)+Ui.S(8);
                row=PlaceText(lblMsg,pad,row,w-pad*2,Ui.S(22));
                c2.Height=row+pad;

                extra.Location=new Point(x,c2.Bottom+Ui.S(16));
                row=PlaceText(h3,pad,pad,w-pad*2,Ui.S(28));
                row=PlaceText(info,pad,row+Ui.S(6),w-pad*2,Ui.S(24));
                bool inline=w>=Ui.S(620);
                credit.SetBounds(pad,row+gap,inline?w-pad*2-redeem.Width-gap:w-pad*2,Ui.S(44));
                redeem.Location=inline?new Point(credit.Right+gap,credit.Top+(credit.Height-redeem.Height)/2):new Point(pad,credit.Bottom+gap);
                row=Math.Max(credit.Bottom,redeem.Bottom)+gap;
                row=PlaceText(contact,pad,row,w-pad*2,Ui.S(24));
                extra.Height=row+pad;
                AutoScrollMinSize=new Size(0,extra.Bottom-AutoScrollPosition.Y+Padding.Bottom);
            } finally {ResumeLayout(false);placing=false;}
        }

        private void Sync()
        {
            var li = LicenseGate.LastInfo; bool ok = li != null && li.Valid;
            chip.Text = ok ? "정상" : "확인 필요"; chip.Tone = ok ? Theme.Success : Theme.Danger; chip.Invalidate();
            lblPc.Text = TpmDevice.DisplayId();
            if (!ok) { lblName.Text = "-"; lblExp.Text = "-"; lblLeft.Text = "-"; return; }
            lblName.Text = "EXP " + li.Edition + (li.Trial ? " · 체험판" : "") + " / " + li.Name;
            if (li.Expires == DateTime.MaxValue) { lblExp.Text = "무기한"; lblLeft.Text = "무기한"; }
            else
            {
                lblExp.Text = li.Expires.ToString("yyyy-MM-dd HH:mm");
                int days = (int)Math.Ceiling((li.Expires.Date.AddDays(1) - DateTime.Now).TotalDays);
                var left = li.Expires.ToUniversalTime() - ServerClock.UtcNow;
                lblLeft.Text = left.TotalHours < 24 ? Math.Max(0,(int)left.TotalHours) + "시간 " + Math.Max(0,left.Minutes) + "분 남음" : days + "일 남음";
            }
        }

        public override void OnActivated() { Sync(); }
    }
}
