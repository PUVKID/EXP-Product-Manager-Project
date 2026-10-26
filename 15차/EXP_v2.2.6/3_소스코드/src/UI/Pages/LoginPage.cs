using System;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>번개장터 로그인 확인. 아이디/비밀번호는 받지 않고, 전용 크롬 창에서 사용자가 직접 로그인한다(기존 방식 그대로).</summary>
    public class LoginPage : PageBase
    {
        private readonly Chip chip;
        public LoginPage(AppHost host) : base(host)
        {
            Key = "login"; PageTitle = "로그인";
            var title = Ui.Title("로그인"); title.Location = new Point(0, 0);

            var card = new CardPanel { Location = new Point(0, 74), Height = 330, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var h = Ui.Heading("번개장터 로그인"); h.Location = new Point(20, 18);
            chip = new Chip { Text = "확인 전" }; chip.Location = new Point(190, 20);
            var bLogin = Ui.Button("로그인 테스트", "\uE77B", BtnStyle.Primary, 160); bLogin.Location = new Point(20, 60);
            var bReset = Ui.Button("로그인 상태 초기화", "\uE777", BtnStyle.Standard, 190); bReset.Location = new Point(190, 60);
            var info = Ui.Muted("1. 로그인 테스트를 누르세요.\r\n\r\n2. 열린 Chrome에서 번개장터에 로그인하세요.\r\n\r\n3. 로그인을 마친 뒤 안내창에서 예를 누르세요.");
            info.Location = new Point(22, 124);
            card.Controls.AddRange(new Control[] { h, chip, bLogin, bReset, info });
            Controls.AddRange(new Control[] { title, card });
            Resize += delegate { card.Width = Math.Max(Ui.S(400), ClientSize.Width - Padding.Horizontal); };

            bLogin.Click += delegate { Host.RunBackground("로그인 테스트", delegate (Bot b) { b.EnsureLogin(); }); };
            bReset.Click += delegate
            {
                Host.Cfg.AssumeLoggedIn = false; Storage.SaveSettings(Host.Cfg);
                Log.Write("로그인 상태를 초기화했습니다. 다음 실행 때 로그인을 다시 확인합니다.");
                Sync(); Host.RaiseLoginChanged();
            };
            Host.LoginChanged += Sync;
            Sync();
        }

        private void Sync()
        {
            bool ok = Host.Cfg.AssumeLoggedIn;
            chip.Text = ok ? "로그인 확인됨" : "로그인 미확인"; chip.Tone = ok ? Theme.Success : Theme.Warn; chip.Invalidate();
        }

        public override void OnActivated() { Sync(); }
    }
}
