using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>
    /// 앱 전체에서 쓰는 밝은 아이보리 + 골드 "프리미엄" 테마.
    /// 주의: WinForms 에서 BackColor=Transparent 를 UserPaint 컨트롤에 쓰면 실제 Windows 에서
    /// 해당 영역이 완전히 검게 칠해지는 고질적인 버그가 있다. 그래서 이 파일에서는
    /// Color.Transparent 를 절대 쓰지 않고, 모든 컨트롤의 배경색을 항상 "실제 색"으로 명시한다.
    /// </summary>
    public static class Theme
    {
        public static readonly Color BgDark = Color.FromArgb(237, 232, 222);     // 바탕(은은한 아이보리)
        public static readonly Color Panel = Color.FromArgb(255, 254, 251);      // 카드/선택된 탭(거의 흰색)
        public static readonly Color PanelAlt = Color.FromArgb(247, 242, 232);   // 교대 행 / 은은한 강조
        public static readonly Color Field = Color.FromArgb(255, 255, 255);      // 입력창
        public static readonly Color FieldBorder = Color.FromArgb(213, 203, 182);
        public static readonly Color Border = Color.FromArgb(222, 214, 196);
        public static readonly Color Gold = Color.FromArgb(160, 120, 44);        // 진한 브라스 골드(밝은 바탕에서도 또렷함)
        public static readonly Color GoldBright = Color.FromArgb(193, 151, 66);
        public static readonly Color GoldDim = Color.FromArgb(118, 87, 28);
        public static readonly Color TextPrimary = Color.FromArgb(46, 39, 31);   // 진한 에스프레소 브라운
        public static readonly Color TextMuted = Color.FromArgb(128, 117, 101);
        public static readonly Color Danger = Color.FromArgb(173, 56, 56);
        public static readonly Color OnGold = Color.FromArgb(255, 252, 244);     // 골드 버튼 위 글자(크림 화이트)

        private const string TagPrimary = "lux-primary";
        private const int ButtonRadius = 7;

        public static Font FontBase() { return new Font("맑은 고딕", 9.3f, FontStyle.Regular); }
        public static Font FontBold() { return new Font("맑은 고딕", 9.3f, FontStyle.Bold); }
        public static Font FontTitle() { return new Font("맑은 고딕", 14f, FontStyle.Bold); }
        public static Font FontSub() { return new Font("맑은 고딕", 9f, FontStyle.Regular); }
        public static Font FontMono(float size) { return new Font("Consolas", size); }

        /// <summary>이 버튼을 강조(골드 채움) 버튼으로 표시한다. Apply() 를 부르기 전에 호출해야 한다.</summary>
        public static void MarkPrimary(Button b) { b.Tag = TagPrimary; }
        private static bool IsPrimary(Button b) { return (b.Tag as string) == TagPrimary; }

        /// <summary>폼과 그 하위 컨트롤 전체에 밝은 아이보리/골드 테마를 재귀적으로 입힌다. 항상 불투명한 색만 사용한다.</summary>
        public static void Apply(Control root)
        {
            if (root is Form)
            {
                root.BackColor = BgDark;
                root.ForeColor = TextPrimary;
            }
            ApplyChildren(root, root.BackColor);
        }

        private static void ApplyChildren(Control parent, Color parentBg)
        {
            foreach (Control c in parent.Controls)
            {
                Color childBg = StyleOne(c, parentBg);
                ApplyChildren(c, childBg);
            }
        }

        /// <summary>컨트롤 하나에 색을 입히고, 그 컨트롤의 "실제 배경색"(자식에게 물려줄 색)을 돌려준다.</summary>
        private static Color StyleOne(Control c, Color parentBg)
        {
            if (c is Button)
            {
                var b = (Button)c;
                bool primary = IsPrimary(b);
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.UseVisualStyleBackColor = false;
                b.Cursor = Cursors.Hand;
                b.Font = FontBold();
                if (b.Height < 30) b.Height = 30;
                if (primary)
                {
                    b.BackColor = Gold;
                    b.ForeColor = OnGold;
                    b.FlatAppearance.BorderColor = GoldDim;
                    b.FlatAppearance.MouseOverBackColor = GoldBright;
                    b.FlatAppearance.MouseDownBackColor = GoldDim;
                }
                else
                {
                    b.BackColor = Field;
                    b.ForeColor = TextPrimary;
                    b.FlatAppearance.BorderColor = FieldBorder;
                    b.FlatAppearance.MouseOverBackColor = PanelAlt;
                    b.FlatAppearance.MouseDownBackColor = Border;
                }
                RoundButton(b);
                return parentBg;   // 버튼 모서리 바깥은 부모 배경이 비치므로, 자식 기준색은 부모색 그대로
            }
            if (c is TextBoxBase)
            {
                c.BackColor = Field;
                c.ForeColor = TextPrimary;
                if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                return c.BackColor;
            }
            if (c is NumericUpDown)
            {
                var n = (NumericUpDown)c;
                n.BorderStyle = BorderStyle.FixedSingle;
                n.BackColor = Field;
                n.ForeColor = TextPrimary;
                return n.BackColor;
            }
            if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Field;
                cb.ForeColor = TextPrimary;
                return cb.BackColor;
            }
            if (c is ListBox)
            {
                c.BackColor = Field;
                c.ForeColor = TextPrimary;
                return c.BackColor;
            }
            if (c is CheckBox)
            {
                c.ForeColor = TextPrimary;
                c.BackColor = parentBg;
                return parentBg;
            }
            if (c is Label)
            {
                var l = (Label)c;
                if (l.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb() || l.ForeColor.ToArgb() == Color.Black.ToArgb())
                    l.ForeColor = TextMuted;
                l.BackColor = parentBg;
                return parentBg;
            }
            if (c is DataGridView)
            {
                StyleGrid((DataGridView)c);
                return Panel;
            }
            if (c is TabControl)
            {
                var tc = (TabControl)c;
                StyleTabs(tc);
                return Panel;
            }
            if (c is TabPage)
            {
                c.BackColor = Panel;
                c.ForeColor = TextPrimary;
                return Panel;
            }
            if (c is SplitContainer)
            {
                var sp = (SplitContainer)c;
                sp.BackColor = Border;
                sp.Panel1.BackColor = Panel;
                sp.Panel2.BackColor = Panel;
                return Panel;
            }
            if (c is ProgressBar)
            {
                return parentBg;
            }

            // Panel / FlowLayoutPanel / TableLayoutPanel / GroupBox 등 일반 컨테이너
            c.BackColor = parentBg;
            if (c.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb()) c.ForeColor = TextPrimary;
            return parentBg;
        }

        /// <summary>버튼 모서리를 살짝 둥글게 깎는다(바깥쪽은 부모 배경이 비친다). 크기가 바뀌어도 다시 적용한다.</summary>
        private static void RoundButton(Button b)
        {
            ApplyRoundRegion(b);
            b.SizeChanged -= OnButtonSizeChanged;
            b.SizeChanged += OnButtonSizeChanged;
        }

        private static void OnButtonSizeChanged(object sender, EventArgs e) { ApplyRoundRegion((Button)sender); }

        private static void ApplyRoundRegion(Button b)
        {
            if (b.Width <= 0 || b.Height <= 0) return;
            var old = b.Region;
            b.Region = new Region(RoundedRect(new Rectangle(0, 0, b.Width, b.Height), ButtonRadius));
            if (old != null) old.Dispose();
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            if (d >= r.Width || d >= r.Height)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
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
            g.ColumnHeadersHeight = 32;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.BackColor = Field;
            g.DefaultCellStyle.ForeColor = TextPrimary;
            g.DefaultCellStyle.SelectionBackColor = Gold;
            g.DefaultCellStyle.SelectionForeColor = OnGold;
            g.AlternatingRowsDefaultCellStyle.BackColor = PanelAlt;
            g.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Gold;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = OnGold;
            g.RowTemplate.Height = 27;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        public static void StyleTabs(TabControl tc)
        {
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.SizeMode = TabSizeMode.Fixed;
            tc.ItemSize = new Size(140, 34);
            tc.Padding = new Point(14, 7);
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
                selected ? Gold : TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>상단에 타이틀/부제를 담은 헤더 바를 만든다. 완전히 불투명한 색만 사용한다.</summary>
        public static Panel BuildHeader(string title, string subtitle)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Panel };

            var accent = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Gold };
            header.Controls.Add(accent);

            var lblTitle = new Label
            {
                Text = title,
                Left = 18,
                Top = 9,
                AutoSize = true,
                Font = FontTitle(),
                ForeColor = TextPrimary,
                BackColor = Panel
            };
            var lblSub = new Label
            {
                Text = subtitle,
                Left = 20,
                Top = 36,
                AutoSize = true,
                Font = FontSub(),
                ForeColor = Gold,
                BackColor = Panel
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);
            return header;
        }
    }
}
