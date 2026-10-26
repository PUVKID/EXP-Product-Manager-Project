using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>앱 전체에서 쓰는 다크 + 골드 "프리미엄" 테마. 색/폰트 상수와 커스텀 컨트롤, 적용 헬퍼를 모아둔다.</summary>
    public static class Theme
    {
        public static readonly Color BgDark = Color.FromArgb(16, 17, 21);
        public static readonly Color Panel = Color.FromArgb(24, 26, 32);
        public static readonly Color PanelAlt = Color.FromArgb(29, 31, 38);
        public static readonly Color Field = Color.FromArgb(33, 36, 44);
        public static readonly Color FieldBorder = Color.FromArgb(58, 62, 73);
        public static readonly Color Border = Color.FromArgb(46, 49, 58);
        public static readonly Color Gold = Color.FromArgb(197, 160, 71);
        public static readonly Color GoldBright = Color.FromArgb(226, 193, 115);
        public static readonly Color GoldDim = Color.FromArgb(120, 98, 48);
        public static readonly Color TextPrimary = Color.FromArgb(232, 233, 237);
        public static readonly Color TextMuted = Color.FromArgb(146, 151, 163);
        public static readonly Color Danger = Color.FromArgb(217, 97, 97);
        public static readonly Color OnGold = Color.FromArgb(26, 22, 14);

        public static Font FontBase() { return new Font("맑은 고딕", 9.3f, FontStyle.Regular); }
        public static Font FontBold() { return new Font("맑은 고딕", 9.3f, FontStyle.Bold); }
        public static Font FontTitle() { return new Font("맑은 고딕", 15.5f, FontStyle.Bold); }
        public static Font FontSub() { return new Font("맑은 고딕", 9f, FontStyle.Regular); }
        public static Font FontMono(float size) { return new Font("Consolas", size); }

        /// <summary>폼과 그 하위 컨트롤 전체에 다크/골드 테마를 재귀적으로 입힌다.</summary>
        public static void Apply(Control root)
        {
            if (root is Form)
            {
                root.BackColor = BgDark;
                root.ForeColor = TextPrimary;
            }
            foreach (Control c in root.Controls)
            {
                StyleOne(c);
                Apply(c);
            }
        }

        private static void StyleOne(Control c)
        {
            if (c is LuxButton) { /* 스스로 색을 관리 */ }
            else if (c is Button)
            {
                var b = (Button)c;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = GoldDim;
                b.FlatAppearance.MouseOverBackColor = PanelAlt;
                b.FlatAppearance.MouseDownBackColor = Field;
                b.BackColor = Field;
                b.ForeColor = TextPrimary;
                b.Cursor = Cursors.Hand;
                b.Font = FontBold();
                if (b.Height < 28) b.Height = 28;
            }
            else if (c is TextBox)
            {
                var t = (TextBox)c;
                t.BorderStyle = BorderStyle.FixedSingle;
                t.BackColor = Field;
                t.ForeColor = TextPrimary;
            }
            else if (c is NumericUpDown)
            {
                var n = (NumericUpDown)c;
                n.BorderStyle = BorderStyle.FixedSingle;
                n.BackColor = Field;
                n.ForeColor = TextPrimary;
            }
            else if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Field;
                cb.ForeColor = TextPrimary;
            }
            else if (c is ListBox)
            {
                c.BackColor = Field;
                c.ForeColor = TextPrimary;
            }
            else if (c is CheckBox)
            {
                c.ForeColor = TextPrimary;
                c.BackColor = Color.Transparent;
            }
            else if (c is Label)
            {
                if (c.ForeColor == SystemColors.ControlText || c.ForeColor.ToArgb() == Color.Black.ToArgb())
                    c.ForeColor = TextMuted;
                c.BackColor = Color.Transparent;
            }
            else if (c is TabControl)
            {
                StyleTabs((TabControl)c);
            }
            else if (c is TabPage)
            {
                c.BackColor = Panel;
                c.ForeColor = TextPrimary;
            }
            else if (c is DataGridView)
            {
                StyleGrid((DataGridView)c);
            }
            else if (c is SplitContainer)
            {
                var sp = (SplitContainer)c;
                sp.BackColor = Border;
                sp.Panel1.BackColor = Panel;
                sp.Panel2.BackColor = Panel;
            }
            else
            {
                c.BackColor = c.Parent != null ? c.Parent.BackColor : Panel;
                if (c.ForeColor == SystemColors.ControlText) c.ForeColor = TextPrimary;
            }
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Panel;
            g.GridColor = Border;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersDefaultCellStyle.BackColor = Panel;
            g.RowHeadersDefaultCellStyle.ForeColor = TextMuted;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = Field;
            g.ColumnHeadersDefaultCellStyle.BackColor = PanelAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Gold;
            g.ColumnHeadersDefaultCellStyle.Font = FontBold();
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = PanelAlt;
            g.ColumnHeadersHeight = 30;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.BackColor = Field;
            g.DefaultCellStyle.ForeColor = TextPrimary;
            g.DefaultCellStyle.SelectionBackColor = Gold;
            g.DefaultCellStyle.SelectionForeColor = OnGold;
            g.AlternatingRowsDefaultCellStyle.BackColor = PanelAlt;
            g.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Gold;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = OnGold;
            g.RowTemplate.Height = 26;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        public static void StyleTabs(TabControl tc)
        {
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.SizeMode = TabSizeMode.Fixed;
            tc.ItemSize = new Size(140, 34);
            tc.Padding = new Point(14, 6);
            tc.DrawItem -= TabDrawItem;
            tc.DrawItem += TabDrawItem;
        }

        private static void TabDrawItem(object sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender;
            var page = tc.TabPages[e.Index];
            var rect = tc.GetTabRect(e.Index);
            bool selected = e.Index == tc.SelectedIndex;
            using (var bg = new SolidBrush(selected ? Panel : BgDark))
                e.Graphics.FillRectangle(bg, rect);
            if (selected)
                using (var gold = new SolidBrush(Gold))
                    e.Graphics.FillRectangle(gold, rect.Left, rect.Bottom - 3, rect.Width, 3);
            TextRenderer.DrawText(e.Graphics, page.Text, selected ? FontBold() : FontBase(), rect,
                selected ? GoldBright : TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>상단에 로고/타이틀/부제를 담은 헤더 바를 만든다.</summary>
        public static Panel BuildHeader(string title, string subtitle)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Panel };
            var accent = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Gold };
            header.Controls.Add(accent);

            var logo = new Panel { Width = 38, Height = 38, Top = 13, Left = 18, BackColor = Color.Transparent };
            logo.Paint += delegate (object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, 38, 38), GoldBright, GoldDim, 45f))
                    e.Graphics.FillEllipse(brush, 0, 0, 37, 37);
                TextRenderer.DrawText(e.Graphics, "B", FontTitle(), new Rectangle(0, 0, 38, 38), OnGold,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            header.Controls.Add(logo);

            var lblTitle = new Label
            {
                Text = title,
                Left = 66,
                Top = 11,
                AutoSize = true,
                Font = FontTitle(),
                ForeColor = TextPrimary,
                BackColor = Color.Transparent
            };
            var lblSub = new Label
            {
                Text = subtitle,
                Left = 68,
                Top = 38,
                AutoSize = true,
                Font = FontSub(),
                ForeColor = Gold,
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);
            return header;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>둥근 모서리 + 호버/클릭 상태를 직접 그리는 버튼. Primary=true 이면 골드 채움(강조) 버튼.</summary>
        public class LuxButton : Button
        {
            public bool Primary;
            private bool _hover, _down;

            public LuxButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                Font = FontBold();
                ForeColor = TextPrimary;
                BackColor = Color.Transparent;
                if (Height < 30) Height = 30;
                MouseEnter += delegate { _hover = true; Invalidate(); };
                MouseLeave += delegate { _hover = false; _down = false; Invalidate(); };
                MouseDown += delegate { _down = true; Invalidate(); };
                MouseUp += delegate { _down = false; Invalidate(); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = RoundedRect(rect, Math.Min(8, Height / 2)))
                {
                    Color fill;
                    Color border;
                    Color text;
                    if (!Enabled)
                    {
                        fill = Field; border = Border; text = TextMuted;
                    }
                    else if (Primary)
                    {
                        fill = _down ? GoldDim : (_hover ? GoldBright : Gold);
                        border = Gold;
                        text = OnGold;
                    }
                    else
                    {
                        fill = _down ? Field : (_hover ? PanelAlt : Field);
                        border = _hover ? Gold : FieldBorder;
                        text = TextPrimary;
                    }
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
                    TextRenderer.DrawText(g, Text, Font, rect, text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        /// <summary>둥근 골드 채움 진행률 표시줄. ProgressBar 와 동일한 Minimum/Maximum/Value API 를 흉내낸다.</summary>
        public class LuxProgressBar : Control
        {
            private int _min, _max = 100, _value;
            public int Minimum { get { return _min; } set { _min = value; Invalidate(); } }
            public int Maximum { get { return _max; } set { _max = Math.Max(value, _min + 1); Invalidate(); } }
            public int Value
            {
                get { return _value; }
                set { _value = Math.Max(_min, Math.Min(_max, value)); Invalidate(); }
            }

            public LuxProgressBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
                Height = 18;
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = RoundedRect(rect, Height / 2))
                {
                    using (var bgBrush = new SolidBrush(Field)) g.FillPath(bgBrush, path);
                    using (var p = new Pen(Border, 1f)) g.DrawPath(p, path);
                }
                double frac = (_max > _min) ? (double)(_value - _min) / (_max - _min) : 0;
                int w = (int)Math.Round(frac * Width);
                if (w > 2)
                {
                    var fillRect = new Rectangle(0, 0, Math.Min(w, Width) - 1, Height - 1);
                    using (var path = RoundedRect(fillRect, Height / 2))
                    using (var grad = new LinearGradientBrush(fillRect, GoldDim, GoldBright, 0f))
                        g.FillPath(grad, path);
                }
            }
        }
    }
}
