using System;
using System.Drawing;
using System.Windows.Forms;
namespace BunjangAuto {
    public class SettingsPage : PageBase {
        readonly Segmented theme; readonly SoftInput chrome; bool loading;
        public SettingsPage(AppHost host):base(host) {
            Key="settings";PageTitle="설정";
            var title=Ui.Title("설정");
            var appearance=new CardPanel {Left=0,Top=62,Height=132};
            appearance.Controls.Add(Design.Label("화면 테마",24,20,360,26,13,"t"));
            theme=new Segmented {Left=24,Top=70,Width=Ui.S(330),Height=Ui.S(34)};theme.Items.AddRange(new[]{"라이트","다크","시스템"});appearance.Controls.Add(theme);
            var browser=new CardPanel {Left=0,Top=210,Height=148};
            browser.Controls.Add(Design.Label("Chrome",24,20,380,28,13,"t"));
            chrome=Ui.Input(480,Host.Cfg.ChromePath??"");chrome.Location=new Point(24,68);
            var browse=Ui.Button("찾아보기","",BtnStyle.Standard,124);browse.Location=new Point(520,68);
            browser.Controls.AddRange(new Control[]{chrome,browse});
            var health=new CardPanel {Left=0,Top=374,Height=156};
            health.Controls.Add(Design.Label("자가진단",24,20,380,28,13,"t"));
            health.Controls.Add(Design.Label("인증, Chrome, 상품 사진과 연결 상태를 확인합니다.",24,59,610,25,10,"m"));
            var diagnose=Ui.Button("진단 시작","",BtnStyle.Primary,160);diagnose.Location=new Point(24,102);health.Controls.Add(diagnose);
            var version=Ui.Muted("EXP "+Brand.Version);version.Location=new Point(2,550);
            Controls.AddRange(new Control[]{title,appearance,browser,health,version});
            Resize+=delegate {int w=Math.Max(Ui.S(660),ClientSize.Width-Padding.Horizontal);appearance.Width=browser.Width=health.Width=w;};
            theme.SelectedIndexChanged+=delegate {if(!loading)ThemeService.SetMode((ThemeMode)theme.SelectedIndex);};
            chrome.Leave+=delegate {Save();};
            browse.Click+=delegate {using(var dialog=new OpenFileDialog {Filter="Chrome|chrome.exe"})if(dialog.ShowDialog(FindForm())==DialogResult.OK){chrome.Text=dialog.FileName;Save();}};
            diagnose.Click+=delegate {Save();using(var dialog=new DiagnosticsForm(Host.Cfg,Host.Products))dialog.ShowDialog(FindForm());};
        }
        void Save(){Host.Cfg.ChromePath=chrome.Text.Trim();Storage.SaveSettings(Host.Cfg);}
        public override void OnActivated(){loading=true;theme.SetIndexSilently((int)ThemeService.Mode);loading=false;chrome.Text=Host.Cfg.ChromePath??"";}
    }
}
