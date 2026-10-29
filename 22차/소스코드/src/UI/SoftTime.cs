using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>날짜 달력 없이 시각을 입력하고 분 단위로 조절하는 둥근 입력입니다.</summary>
    public class SoftTime : UserControl, IThemed
    {
        sealed class TimeEditor : TextBox, IThemed
        {
            public void ApplyTheme() { BorderStyle = BorderStyle.None; BackColor = Theme.Field; ForeColor = Theme.TextPrimary; Font = Theme.FontBase(); }
        }
        readonly TextBox editor;
        readonly SoftButton minus, plus;
        int minutes;
        bool syncing;
        public bool AllowEndOfDay { get; set; }
        public event EventHandler ValueChanged;
        public string Value
        {
            get { return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"); }
            set { int parsed; if (TryRead(value, out parsed)) SetMinute(parsed); else Sync(); }
        }
        public SoftTime()
        {
            AutoScaleMode = AutoScaleMode.None; DoubleBuffered = true; TabStop = false;
            editor = new TimeEditor { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center, MaxLength = 5, AccessibleName = "시각 HH:mm" };
            minus = new SoftButton { Text = "−", TabStop = false }; plus = new SoftButton { Text = "+", TabStop = false };
            minus.Click += delegate { Step(-1); }; plus.Click += delegate { Step(1); };
            editor.TextChanged += delegate { int parsed; if (!syncing && TryRead(editor.Text, out parsed) && parsed != minutes) { minutes = parsed; Changed(); } };
            editor.Validated += delegate { Sync(); };
            editor.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Step(e.KeyCode == Keys.Up ? 1 : -1); e.SuppressKeyPress = true; } };
            editor.GotFocus += delegate { Invalidate(); }; editor.LostFocus += delegate { Invalidate(); };
            Controls.AddRange(new Control[] { editor, minus, plus }); Sync(); ApplyTheme();
        }
        bool TryRead(string text, out int result)
        {
            result = 0;
            if (AllowEndOfDay && text == "24:00") { result = 1440; return true; }
            TimeSpan parsed;
            if (text == null || text.Length != 5 || !TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out parsed)) return false;
            result = (int)parsed.TotalMinutes; return result < 1440;
        }
        void Step(int delta) { SetMinute(Math.Max(0, Math.Min(AllowEndOfDay ? 1440 : 1439, minutes + delta))); }
        void SetMinute(int value) { bool changed = minutes != value; minutes = value; Sync(); if (changed) Changed(); }
        void Changed() { var h = ValueChanged; if (h != null) h(this, EventArgs.Empty); }
        void Sync() { if (editor == null) return; syncing = true; editor.Text = Value; syncing = false; }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (editor == null) return;
            int w = Dpi.S(30), gap = Dpi.S(5);
            minus.SetBounds(gap, gap, w, Height - gap * 2); plus.SetBounds(Width - w - gap, gap, w, Height - gap * 2);
            editor.SetBounds(w + gap * 2, (Height - editor.PreferredHeight) / 2, Math.Max(20, Width - w * 2 - gap * 4), editor.PreferredHeight);
        }
        public void ApplyTheme() { BackColor = Theme.Field; ForeColor = Theme.TextPrimary; if (editor != null) { editor.BackColor = Theme.Field; editor.ForeColor = Theme.TextPrimary; editor.Font = Theme.FontBase(); editor.BorderStyle = BorderStyle.None; minus.ApplyTheme(); plus.ApplyTheme(); } Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) { SoftInput.PaintSurface(e.Graphics, this, ContainsFocus); }
    }
}
