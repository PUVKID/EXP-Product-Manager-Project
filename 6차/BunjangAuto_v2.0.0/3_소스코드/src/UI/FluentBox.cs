using System;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>MessageBox.Show 와 같은 모양의 호출을 받는 테마 대화상자(다크 모드에서도 어색하지 않게).</summary>
    public static class FluentBox
    {
        public static DialogResult Show(string text, string caption) { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons b, MessageBoxIcon i) { return Show(null, text, caption, b, i); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b) { return Show(owner, text, caption, b, MessageBoxIcon.None); }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var f = new BoxForm(text, caption, buttons, icon))
                return owner != null ? f.ShowDialog(owner) : f.ShowDialog();
        }

        private class BoxForm : Form
        {
            public BoxForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            {
                Text = caption; Font = Theme.FontBase();
                FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterParent;
                AutoScaleMode = AutoScaleMode.Dpi;
                int textW = 420;
                var sz = TextRenderer.MeasureText(text, Theme.FontBase(), new Size(textW, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                int glyphW = icon != MessageBoxIcon.None ? 44 : 0;
                int h = Math.Max(60, sz.Height) + 24;
                ClientSize = new Size(Math.Max(380, textW + glyphW + 48), h + 90);
                var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
                var bar = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = Theme.BgDark };
                Controls.Add(body); Controls.Add(bar);
                if (glyphW > 0)
                {
                    string g = icon == MessageBoxIcon.Error ? "\uEA39" : icon == MessageBoxIcon.Warning ? "\uE7BA" : icon == MessageBoxIcon.Question ? "\uE897" : "\uE946";
                    Color c = icon == MessageBoxIcon.Error ? Theme.Danger : icon == MessageBoxIcon.Warning ? Theme.Warn : Theme.Accent;
                    body.Controls.Add(new Label { Left = 20, Top = 20, Width = 36, Height = 36, Text = Theme.HasIconFont ? g : "!", Font = Theme.HasIconFont ? Theme.IconFont(20f) : Theme.FontTitle(), ForeColor = c, BackColor = Theme.Panel });
                }
                body.Controls.Add(new Label { Left = 24 + glyphW, Top = 20, Width = textW, Height = h - 8, Text = text, ForeColor = Theme.TextPrimary, BackColor = Theme.Panel, Font = Theme.FontBase() });

                string[] names; DialogResult[] results;
                switch (buttons)
                {
                    case MessageBoxButtons.YesNo: names = new[] { "예", "아니오" }; results = new[] { DialogResult.Yes, DialogResult.No }; break;
                    case MessageBoxButtons.OKCancel: names = new[] { "확인", "취소" }; results = new[] { DialogResult.OK, DialogResult.Cancel }; break;
                    case MessageBoxButtons.YesNoCancel: names = new[] { "예", "아니오", "취소" }; results = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }; break;
                    default: names = new[] { "확인" }; results = new[] { DialogResult.OK }; break;
                }
                int x = ClientSize.Width - 16;
                for (int i = names.Length - 1; i >= 0; i--)
                {
                    var b = new FluentButton { Text = names[i], Width = 96, Height = 34, Style = i == 0 ? BtnStyle.Primary : BtnStyle.Standard };
                    b.Left = x - b.Width; b.Top = 14; x = b.Left - 8;
                    var res = results[i];
                    b.Click += delegate { DialogResult = res; Close(); };
                    bar.Controls.Add(b);
                }
                CancelButton = null;
                KeyPreview = true;
                KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = results.Length == 1 ? results[0] : (Array.IndexOf(results, DialogResult.Cancel) >= 0 ? DialogResult.Cancel : DialogResult.No); Close(); } };
                BackColor = Theme.BgDark;
                Theme.Apply(this);
            }
        }
    }
}
