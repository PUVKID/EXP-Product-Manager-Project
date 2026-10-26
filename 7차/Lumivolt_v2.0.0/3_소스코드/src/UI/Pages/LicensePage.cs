using System;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>라이선스 센터: 상태·이름·만료일·남은 기간·PC 고유번호(복사)·키 교체. 검증은 기존 License.Verify 를 그대로 쓴다.</summary>
    public class LicensePage : PageBase
    {
        private readonly Label lblStatus, lblName, lblExp, lblLeft, lblPc, lblMsg;
        private readonly TextBox txtKey;
        private readonly Chip chip;

        public LicensePage(AppHost host) : base(host)
        {
            Key = "license"; PageTitle = "라이선스";
            var title = Ui.Title("라이선스"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("이 PC 한 대에서 기간 동안만 사용할 수 있는 라이선스입니다."); sub.Location = new Point(2, 38);

            var c1 = new CardPanel { Location = new Point(0, 74), Height = 230, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h1 = Ui.Heading("라이선스 정보"); h1.Location = new Point(20, 16);
            chip = new Chip { Text = "확인 중" }; chip.Location = new Point(160, 18);
            int y = 56; Func<string, Label> row = cap =>
            {
                var a = Ui.Muted(cap); a.Location = new Point(22, y);
                var b = Ui.Text("-", 10f, true); b.Location = new Point(150, y - 2);
                c1.Controls.Add(a); c1.Controls.Add(b); y += 32; return b;
            };
            lblName = row("라이선스 이름"); lblExp = row("만료일"); lblLeft = row("남은 기간");
            lblPc = row("PC 고유번호"); lblPc.Font = Theme.FontMono(11f);
            var bCopy = Ui.Button("복사", "\uE8C8", BtnStyle.Standard, 90); bCopy.Location = new Point(330, 177);
            lblStatus = lblName; // (호환용)
            c1.Controls.AddRange(new Control[] { h1, chip, bCopy });

            var c2 = new CardPanel { Location = new Point(0, 316), Height = 210, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h2 = Ui.Heading("라이선스 키 변경 / 연장"); h2.Location = new Point(20, 16);
            var d2 = Ui.Muted("관리자에게 받은 새 키를 붙여 넣고 [인증]을 누르세요. 기간이 연장된 키도 같은 방법입니다."); d2.Location = new Point(22, 46);
            txtKey = new TextBox { Location = new Point(22, 76), Width = 560, Height = 64, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Theme.FontMono(9f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var bOk = Ui.Button("인증", "\uE73E", BtnStyle.Primary, 110); bOk.Location = new Point(22, 152);
            lblMsg = Ui.Text("", 9.5f, false, "m"); lblMsg.Location = new Point(146, 160);
            c2.Controls.AddRange(new Control[] { h2, d2, txtKey, bOk, lblMsg });

            Controls.AddRange(new Control[] { title, sub, c1, c2 });
            Resize += delegate { int w = Math.Max(Ui.S(400), ClientSize.Width - Padding.Horizontal); c1.Width = w; c2.Width = w; txtKey.Width = w - Ui.S(44); };

            bCopy.Click += delegate { try { Clipboard.SetText(License.GetMachineId()); lblMsg.Text = "PC 고유번호를 복사했습니다."; lblMsg.Tag = "m"; lblMsg.ForeColor = Theme.TextMuted; } catch { } };
            bOk.Click += delegate
            {
                string msg;
                bool ok = LicenseGate.TryActivate(txtKey.Text, out msg);
                lblMsg.Text = msg; lblMsg.Tag = ok ? "s" : "d"; lblMsg.ForeColor = ok ? Theme.Success : Theme.Danger;
                if (ok) { txtKey.Text = ""; Sync(); }
            };
        }

        private void Sync()
        {
            var li = LicenseGate.LastInfo; bool ok = li != null && li.Valid;
            chip.Text = ok ? "정상" : "확인 필요"; chip.Tone = ok ? Theme.Success : Theme.Danger; chip.Invalidate();
            lblPc.Text = License.GetMachineId();
            if (!ok) { lblName.Text = "-"; lblExp.Text = "-"; lblLeft.Text = "-"; return; }
            lblName.Text = li.Name ?? "-";
            if (li.Expires == DateTime.MaxValue) { lblExp.Text = "무기한"; lblLeft.Text = "무기한"; }
            else
            {
                lblExp.Text = li.Expires.ToString("yyyy-MM-dd");
                int days = (int)Math.Ceiling((li.Expires.Date.AddDays(1) - DateTime.Now).TotalDays);
                lblLeft.Text = days > 0 ? days + "일 남음" : "만료됨";
            }
        }

        public override void OnActivated() { Sync(); }
    }
}
