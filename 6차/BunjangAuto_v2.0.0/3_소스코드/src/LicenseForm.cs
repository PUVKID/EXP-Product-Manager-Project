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
        private static string SeenPath { get { return Path.Combine(Paths.Root, "license.seen"); } }

        public static bool Check()
        {
            string id = License.GetMachineId();
            string msg = "";
            if (File.Exists(KeyPath))
            {
                var info = License.Verify(File.ReadAllText(KeyPath, Encoding.UTF8), id);
                if (info.Valid && !ClockRolledBack()) { Touch(); LastInfo = info; Log.Write("INFO", "License", "license.verify", "라이선스 확인됨 (" + info.Message + ")", "OK"); return true; }
                msg = info.Valid ? "시스템 날짜가 과거로 바뀌었습니다." : info.Message;
            }
            using (var f = new LicenseForm(id, msg))
            {
                if (f.ShowDialog() != DialogResult.OK) return false;
                File.WriteAllText(KeyPath, f.Key, Encoding.UTF8);
                Touch();
                LastInfo = License.Verify(f.Key, id);
                Log.Write("INFO", "License", "license.verify", "라이선스를 새로 인증했습니다.", "OK");
                return true;
            }
        }

        /// <summary>License 화면에서 키를 바꿀 때 사용. 검증은 예전과 같은 License.Verify 로 하고, 통과하면 같은 위치에 저장한다.</summary>
        public static bool TryActivate(string key, out string message)
        {
            var info = License.Verify(key, License.GetMachineId());
            message = info.Valid ? "인증되었습니다. (" + info.Message + ")" : info.Message;
            if (!info.Valid) { Log.Write("WARN", "License", "license.verify", "라이선스 인증 실패: " + info.Message, "FAIL"); return false; }
            try
            {
                Directory.CreateDirectory(Paths.Root);
                File.WriteAllText(KeyPath, key.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", ""), Encoding.UTF8);
            }
            catch (Exception ex) { message = "키를 저장하지 못했습니다: " + ex.Message; return false; }
            Touch(); LastInfo = info;
            Log.Write("INFO", "License", "license.verify", "라이선스를 새로 인증했습니다.", "OK");
            return true;
        }

        /// <summary>실행 중 주기적으로 호출: 저장된 키를 다시 검증한다.</summary>
        public static bool Recheck()
        {
            try
            {
                if (!File.Exists(KeyPath)) return false;
                var info = License.Verify(File.ReadAllText(KeyPath, Encoding.UTF8), License.GetMachineId());
                if (!info.Valid || ClockRolledBack()) return false;
                Touch();
                LastInfo = info;
                return true;
            }
            catch { return false; }
        }

        private static bool ClockRolledBack()
        {
            try
            {
                if (!File.Exists(SeenPath)) return false;
                var seen = DateTime.Parse(File.ReadAllText(SeenPath), null, System.Globalization.DateTimeStyles.RoundtripKind);
                return DateTime.Now < seen.AddDays(-1);
            }
            catch { return false; }
        }

        private static void Touch()
        {
            try { File.WriteAllText(SeenPath, DateTime.Now.ToString("o")); } catch { }
        }
    }

    public class LicenseForm : Form
    {
        public string Key = "";

        public LicenseForm(string machineId, string message)
        {
            Text = "라이선스 인증 - © 음악천재";
            Width = 560; Height = 420;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Font = Theme.FontBase();

            var header = Theme.BuildHeader("라이선스 인증", "BunjangAuto   ·   © 음악천재");
            var lblMsg = new Label { Left = 20, Top = 76, Width = 510, Height = 36, ForeColor = Theme.Danger, Font = Theme.FontBold(), Text = message };
            var lbl1 = new Label { Left = 20, Top = 116, AutoSize = true, ForeColor = Theme.TextMuted, Text = "이 PC의 고유번호 (관리자에게 전달해 주세요)" };
            var txtId = new TextBox { Left = 20, Top = 138, Width = 374, ReadOnly = true, Text = machineId, Font = Theme.FontMono(11f) };
            var bCopy = new Button { Left = 404, Top = 136, Width = 126, Height = 28, Text = "복사" };
            var lbl2 = new Label { Left = 20, Top = 180, AutoSize = true, ForeColor = Theme.TextMuted, Text = "라이선스 키" };
            var txtKey = new TextBox { Left = 20, Top = 202, Width = 510, Height = 110, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = Theme.FontMono(9.5f) };
            var ok = new Button { Left = 320, Top = 330, Width = 100, Height = 32, Text = "인증" };
            var cancel = new Button { Left = 430, Top = 330, Width = 100, Height = 32, Text = "종료", DialogResult = DialogResult.Cancel };
            var lblCopy = new Label { Left = 20, Top = 372, Width = 510, Height = 20, ForeColor = Theme.TextMuted, Font = new Font(Theme.FontBase().FontFamily, 7.8f), Text = "Copyright © 음악천재. All rights reserved. 무단 복제·유포 시 법적 책임이 따릅니다." };
            Controls.AddRange(new Control[] { lblCopy, lblMsg, lbl1, txtId, bCopy, lbl2, txtKey, ok, cancel, header });
            CancelButton = cancel;
            Theme.MarkPrimary(ok);
            Theme.Apply(this);

            bCopy.Click += delegate { try { Clipboard.SetText(machineId); } catch { } };
            ok.Click += delegate
            {
                var info = License.Verify(txtKey.Text, machineId);
                if (!info.Valid) { lblMsg.Text = info.Message; return; }
                Key = txtKey.Text.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
                FluentBox.Show(this, "인증되었습니다. (" + info.Message + ")", "라이선스");
                DialogResult = DialogResult.OK;
            };
        }
    }
}
