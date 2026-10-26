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
        private static string KeyPath { get { return Path.Combine(Paths.Root, "license.key"); } }
        private static string SeenPath { get { return Path.Combine(Paths.Root, "license.seen"); } }

        public static bool Check()
        {
            string id = License.GetMachineId();
            string msg = "";
            if (File.Exists(KeyPath))
            {
                var info = License.Verify(File.ReadAllText(KeyPath, Encoding.UTF8), id);
                if (info.Valid && !ClockRolledBack()) { Touch(); return true; }
                msg = info.Valid ? "시스템 날짜가 과거로 바뀌었습니다." : info.Message;
            }
            using (var f = new LicenseForm(id, msg))
            {
                if (f.ShowDialog() != DialogResult.OK) return false;
                File.WriteAllText(KeyPath, f.Key, Encoding.UTF8);
                Touch();
                return true;
            }
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
            Width = 560; Height = 390;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Malgun Gothic", 9f);

            var lblMsg = new Label { Left = 16, Top = 12, Width = 510, Height = 36, ForeColor = Color.Firebrick, Text = message };
            var lbl1 = new Label { Left = 16, Top = 52, AutoSize = true, Text = "이 PC의 고유번호 (관리자에게 전달해 주세요)" };
            var txtId = new TextBox { Left = 16, Top = 74, Width = 380, ReadOnly = true, Text = machineId, Font = new Font("Consolas", 11f) };
            var bCopy = new Button { Left = 406, Top = 72, Width = 120, Height = 28, Text = "복사" };
            var lbl2 = new Label { Left = 16, Top = 116, AutoSize = true, Text = "라이선스 키" };
            var txtKey = new TextBox { Left = 16, Top = 138, Width = 510, Height = 110, Multiline = true, ScrollBars = ScrollBars.Vertical };
            var ok = new Button { Left = 316, Top = 268, Width = 100, Height = 30, Text = "인증" };
            var cancel = new Button { Left = 426, Top = 268, Width = 100, Height = 30, Text = "종료", DialogResult = DialogResult.Cancel };
            var lblCopy = new Label { Left = 16, Top = 310, Width = 510, Height = 20, ForeColor = Color.Gray, Text = "Copyright © 음악천재. All rights reserved. 무단 복제·유포 시 법적 책임이 따릅니다." };
            Controls.AddRange(new Control[] { lblCopy, lblMsg, lbl1, txtId, bCopy, lbl2, txtKey, ok, cancel });
            CancelButton = cancel;

            bCopy.Click += delegate { try { Clipboard.SetText(machineId); } catch { } };
            ok.Click += delegate
            {
                var info = License.Verify(txtKey.Text, machineId);
                if (!info.Valid) { lblMsg.Text = info.Message; return; }
                Key = txtKey.Text.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
                MessageBox.Show(this, "인증되었습니다. (" + info.Message + ")", "라이선스");
                DialogResult = DialogResult.OK;
            };
        }
    }
}
