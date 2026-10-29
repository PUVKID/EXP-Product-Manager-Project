using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>시작 시 라이선스를 확인하고, 없거나 잘못되면 입력 창을 띄운다.</summary>
    public static class LicenseGate
    {
        /// <summary>가장 최근에 검증에 성공한 라이선스 정보(대시보드/관리자 정보용). 검증 로직 자체는 그대로다.</summary>
        public static LicenseInfo LastInfo;

        private static string KeyPath { get { return Path.Combine(Paths.Root, "license.key"); } }

        public static bool Check()
        {
            string msg = "";
            if (Recheck()) return true;
            if (File.Exists(KeyPath)) msg = "기존 라이선스를 확인할 수 없습니다. 새 TPM 라이선스를 등록해 주세요.";
            using (var f = new LicenseForm(TpmDevice.DisplayId(), msg))
            {
                if (f.ShowDialog() != DialogResult.OK) return false;
                return Recheck();
            }
        }

        /// <summary>License 화면에서 키를 바꿀 때 사용. 검증은 예전과 같은 License.Verify 로 하고, 통과하면 같은 위치에 저장한다.</summary>
        public static bool TryActivate(string key, out string message)
        {
            var info = License.Verify(key, License.GetMachineId());
            message = info.Valid ? "인증되었습니다. (" + info.Message + ")" : info.Message;
            if (info.Valid && ClockRolledBack()) { message = "시스템 날짜 또는 인증 기록을 확인해 주세요."; return false; }
            if (!info.Valid) { LastInfo = null; Log.Write("WARN", "License", "license.verify", "라이선스 인증 실패: " + info.Message, "FAIL"); return false; }
            try
            {
                UsageQuota.AcceptTrial(info);
                Directory.CreateDirectory(Paths.Root);
                File.WriteAllText(KeyPath, Lumivolt.Security.LicenseText.Normalize(key, "LV3"), Encoding.UTF8);
            }
            catch (Exception ex) { message = "키를 저장하지 못했습니다: " + ex.Message; return false; }
            if (!LicenseClock.Accept(true)) { message = "인증 시각 기록을 저장하지 못했습니다."; return false; } LastInfo = info;
            Log.Write("INFO", "License", "license.verify", "라이선스를 새로 인증했습니다.", "OK");
            return true;
        }

        /// <summary>실행 중 주기적으로 호출: 저장된 키를 다시 검증한다.</summary>
        public static bool Recheck()
        {
            try
            {
                LastInfo = null;
                if (!File.Exists(KeyPath)) return false;
                var info = License.Verify(Lumivolt.Security.LicenseProtocol.ReadFile(KeyPath), License.GetMachineId());
                if (!info.Valid || ClockRolledBack()) return false;
                if (!LicenseClock.Accept(true)) return false;
                UsageQuota.AcceptTrial(info);
                LastInfo = info;
                return true;
            }
            catch { return false; }
        }

        private static bool ClockRolledBack() { return !LicenseClock.Accept(false); }
    }

    public class LicenseForm : Form
    {
        public string Key = "";
        public LicenseForm(string machineId, string message)
        {
            Text = Brand.Name + " · 시작하기"; ClientSize = new Size(620, 768);
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; Font = Theme.FontBase();
            Controls.Add(new BrandMark { Left = 32, Top = 30, Width = 44, Height = 44 });
            Controls.Add(Design.Label(Brand.Name, 92, 30, 300, 34, 23, "t"));
            Controls.Add(Design.Label("한 번 인증하고, 바로 시작하세요.", 34, 88, 548, 30, 17, "t"));
            Controls.Add(Design.Label("파일 없이 복사와 붙여넣기만 하면 됩니다.", 34, 126, 548, 24, 10, "m"));
            var first = new SoftCard { Left = 32, Top = 174, Width = 556, Height = 148 };
            first.Controls.Add(Design.Label("01   등록코드를 판매자에게 보내기", 24, 20, 500, 28, 12, "t"));
            var request = Design.Input("requestCode", 24, 58, 318, 60, true); request.Text = "등록코드 복사를 눌러 주세요.";
            var copy = Design.Button("등록코드 복사", 358, 58, 172, true); copy.Name = "copyRequest";
            first.Controls.AddRange(new Control[] { request, copy });
            var second = new SoftCard { Left = 32, Top = 338, Width = 556, Height = 192 };
            second.Controls.Add(Design.Label("02   받은 인증키로 시작하기", 24, 20, 500, 28, 12, "t"));
            var input = Design.Input("licenseKey", 24, 58, 506, 66, false);
            var paste = Design.Button("인증키 붙여넣기", 24, 138, 174, false); paste.Name = "pasteLicense";
            var activate = Design.Button("인증하고 시작", 356, 138, 174, true); activate.Name = "activateLicense";
            var status = Design.Label(message, 34, 542, 552, 42, 9.5f, "m"); status.Name = "licenseStatus";
            var file = new LinkLabel { Text = "파일로 받은 경우", Left = 36, Top = 588, Width = 130, Font = Theme.Font(9f, FontStyle.Bold) };
            var close = new LinkLabel { Text = "닫기", Left = 544, Top = 588, Width = 50, Font = Theme.Font(9f, FontStyle.Bold) };
            close.LinkClicked += delegate { DialogResult = DialogResult.Cancel; };
            Action<string,bool> notice = (text, good) => { status.Text = text; status.Tag = good ? "s" : "d"; status.ForeColor = good ? Theme.Success : Theme.Danger; };
            copy.Click += delegate { string token, msg; bool ok = LicenseFiles.CopyRequest(out token, out msg); if (token.Length > 0) request.Text = token; notice(msg, ok); };
            paste.Click += delegate { try { input.Text = LicenseFiles.PasteKey(); notice("인증키를 붙여 넣었습니다. 인증하고 시작을 눌러 주세요.", true); } catch (Exception ex) { notice(ex is FormatException ? ex.Message : "클립보드를 읽지 못했습니다. 직접 붙여 넣어 주세요.", false); } };
            activate.Click += delegate { string msg; if (!LicenseGate.TryActivate(input.Text, out msg)) { notice(msg, false); return; } Key = Lumivolt.Security.LicenseText.Normalize(input.Text, "LV3"); DialogResult = DialogResult.OK; };
            file.LinkClicked += delegate { string token = LicenseFiles.ImportLicense(this); if (token != null) input.Text = token; };
            second.Controls.AddRange(new Control[] { input, paste, activate });
            Controls.AddRange(new Control[] { first, second, status, file, close });
            var trial = new SoftCard { Left=32,Top=578,Width=556,Height=142 };
            trial.Controls.Add(Design.Label("Pro 3시간 체험하기",24,16,500,26,12,"t"));
            trial.Controls.Add(Design.Label("요청 내용을 복사해 아래 이메일로 보내 주세요.\r\n받은 체험 키를 위에 입력하면 됩니다. PC·신청 이메일당 1회.",24,48,500,42,9.5f,"m"));
            var requestTrial=Design.Button("체험판 요청 복사",24,96,180,false); requestTrial.Height=32;
            var email=Design.Label(Brand.Email,214,102,320,24,10,"a");
            requestTrial.Click+=delegate { try { var code=TpmDevice.CreateRequest(); Clipboard.SetText("체험판 기능 사용하기\r\nPro 3시간 체험판을 신청합니다.\r\n등록코드: "+code); request.Text=code; notice("요청을 복사했습니다. "+Brand.Email+"로 보내 주세요.",true); } catch(Exception ex) { notice(TpmDevice.Error(ex),false); } };
            trial.Controls.AddRange(new Control[]{requestTrial,email}); Controls.Add(trial); file.Top=close.Top=736;
            AcceptButton = activate; Design.Scale(this); Theme.Apply(this);
            file.LinkColor = close.LinkColor = Theme.Accent;
        }
    }
}
