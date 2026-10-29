using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>화면 배율(DPI) 계산. 관리자 발급툴에서도 쓰도록 Theme.cs 안에 둔다.</summary>
    public static class Dpi
    {
        private static double _scale;
        /// <summary>개발/검증 전용: 0 이 아니면 이 배율을 쓴다(Windows 가 아닌 환경에서 125~200% 를 흉내 내는 용도). 실제 실행에서는 0.</summary>
        public static double ForcedScale;
        /// <summary>개발/검증 전용: 글꼴 크기도 배율만큼 키운다(Windows 에서는 글꼴이 DPI 에 따라 저절로 커지므로 false).</summary>
        public static bool EmulateFontScale;

        /// <summary>화면 배율(100%=1.0, 150%=1.5 ...). 시스템 DPI 기준으로 한 번만 계산한다.</summary>
        public static double Scale
        {
            get
            {
                if (ForcedScale > 0) return ForcedScale;
                if (_scale > 0) return _scale;
                try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) _scale = g.DpiX / 96.0; } catch { _scale = 1.0; }
                if (_scale < 1.0) _scale = 1.0;
                return _scale;
            }
        }

        /// <summary>96dpi 기준 픽셀을 현재 배율로 환산</summary>
        public static int S(int px) { return (int)Math.Round(px * Scale); }
    }

    /// <summary>
    /// 앱 전용 글꼴: 나눔고딕 Bold(SIL OFL 1.1, NAVER). TTF 가 exe 안에 내장되어 있어 따로 설치하지 않아도 된다.
    /// 시작할 때(어떤 글꼴 객체보다 먼저) 파일로 꺼내 이 프로세스에만 보이도록 등록(AddFontResourceEx FR_PRIVATE)하므로
    /// GDI+(Font)와 GDI(TextRenderer) 양쪽에서 쓸 수 있다. 등록에 실패하면 시스템 글꼴(Segoe UI 등)로 자동 대체된다.
    /// 내장 글꼴이 Bold 한 종류뿐이라서, 이 글꼴을 쓰는 동안에는 항상 Bold 스타일로 만든다.
    /// </summary>
    public static class AppFont
    {
        public const string Family = "NanumGothic";
        public static bool Active;
        public static string Status = "";
        private static bool _init;

        [DllImport("gdi32.dll", EntryPoint = "AddFontResourceExW", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        public static void Init()
        {
            if (_init) return;
            _init = true;
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    using (var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("font.NanumGothicBold.ttf"))
                    {
                        if (s != null)
                        {
                            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumivolt", "fonts");
                            System.IO.Directory.CreateDirectory(dir);
                            string path = System.IO.Path.Combine(dir, "NanumGothicBold.ttf");
                            if (!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length != s.Length)
                                using (var o = System.IO.File.Create(path)) s.CopyTo(o);
                            int n = AddFontResourceEx(path, 0x10 /* FR_PRIVATE */, IntPtr.Zero);
                            if (n == 0) Status = "AddFontResourceEx 실패";
                        }
                        else Status = "글꼴 리소스 없음";
                    }
                }
            }
            catch (Exception ex) { Status = ex.GetType().Name + ": " + ex.Message; }
            Active = Probe();
            if (!Active && Status.Length == 0) Status = "글꼴을 찾을 수 없음";
        }

        private static bool Probe()
        {
            try { using (var f = new Font(Family, 10f, FontStyle.Bold, GraphicsUnit.Point)) return string.Equals(f.Name, Family, StringComparison.OrdinalIgnoreCase) || f.Name == "나눔고딕"; }
            catch { return false; }
        }
    }

    /// <summary>한 가지 테마(Light 또는 Dark)의 색 토큰 모음. Windows 11 Fluent 의 계층(바탕 → 사이드바 → 카드 → 입력칸)을 따른다.</summary>
    public class ThemePalette
    {
        public bool Dark;
        public Color Bg;            // 앱 바탕(Mica 에 해당하는 가장 낮은 층)
        public Color Sidebar;       // 사이드바
        public Color Card;          // 카드/패널(바탕 위의 한 층)
        public Color CardAlt;       // 카드 안의 교대 행/은은한 강조
        public Color Field;         // 입력칸
        public Color FieldBorder;
        public Color Border;
        public Color Hover;         // 항목에 마우스를 올렸을 때
        public Color Selected;      // 선택된 사이드바 항목 배경
        public Color Accent;
        public Color AccentHover;
        public Color AccentPressed;
        public Color OnAccent;
        public Color Text;
        public Color TextMuted;
        public Color Danger;
        public Color Success;
        public Color Warn;

        public static readonly ThemePalette Light = new ThemePalette
        {
            Dark = false, Bg = Color.FromArgb(245,246,250), Sidebar = Color.FromArgb(236,239,246),
            Card = Color.White, CardAlt = Color.FromArgb(248,249,251), Field = Color.FromArgb(249,250,252),
            FieldBorder = Color.FromArgb(210,214,222), Border = Color.FromArgb(224,227,233),
            Hover = Color.FromArgb(228,233,241), Selected = Color.FromArgb(216,228,247),
            Accent = Color.FromArgb(31,88,225), AccentHover = Color.FromArgb(24,77,184), AccentPressed = Color.FromArgb(18,62,153), OnAccent = Color.White,
            Text = Color.FromArgb(27,30,37), TextMuted = Color.FromArgb(102,110,125),
            Danger = Color.FromArgb(180,48,58), Success = Color.FromArgb(28,116,82), Warn = Color.FromArgb(142,97,20)
        };
        public static readonly ThemePalette DarkPal = new ThemePalette
        {
            Dark = true, Bg = Color.FromArgb(16,19,28), Sidebar = Color.FromArgb(13,16,24),
            Card = Color.FromArgb(28,33,47), CardAlt = Color.FromArgb(36,40,48), Field = Color.FromArgb(23,26,32),
            FieldBorder = Color.FromArgb(68,76,92), Border = Color.FromArgb(47,53,64),
            Hover = Color.FromArgb(42,49,62), Selected = Color.FromArgb(40,60,94),
            Accent = Color.FromArgb(151,185,255), AccentHover = Color.FromArgb(177,203,255), AccentPressed = Color.FromArgb(123,162,236), OnAccent = Color.FromArgb(16,31,60),
            Text = Color.FromArgb(244,246,251), TextMuted = Color.FromArgb(168,176,192),
            Danger = Color.FromArgb(255,157,165), Success = Color.FromArgb(122,215,177), Warn = Color.FromArgb(236,196,124)
        };
    }

    /// <summary>Windows 11 창 꾸밈(DWM): 어두운 제목 표시줄, 제목 표시줄 색을 앱 바탕색에 맞추기, 둥근 모서리. Windows 10 이하/Mono 에서는 조용히 무시된다.</summary>
    public static class Dwm
    {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        private static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static void Apply(Form f, ThemePalette p)
        {
            if (f == null || !f.IsHandleCreated) return;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try
            {
                int v = p.Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4);
                int round = 2;   // DWMWCP_ROUND
                DwmSetWindowAttribute(f.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
                int cap = Bgr(p.Bg);
                DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref cap, 4);
                int txt = Bgr(p.Text);
                DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref txt, 4);
            }
            catch { }
        }
    }

    /// <summary>
    /// 앱 전체가 공유하는 테마 리소스. 현재 팔레트(Light/Dark)의 색을 속성으로 내주므로,
    /// 팔레트만 바꾸면 이 속성을 쓰는 모든 화면(기존 대화상자 포함)이 같은 색을 쓴다.
    /// (예전 이름 BgDark/Panel/Gold... 는 그대로 유지해 EditForm·LicenseForm·Terms·LicenseAdmin 이 수정 없이 동작한다.)
    /// 주의: UserPaint 컨트롤에 Color.Transparent 를 쓰면 일부 Windows 에서 검게 칠해지므로 항상 실제 색을 쓴다.
    /// </summary>
    public static class Theme
    {
        static Theme() { AppFont.Init(); }   // 관리자 발급툴처럼 Main 에서 Init 을 못 부르는 곳을 위한 안전장치

        public static ThemePalette Current = ThemePalette.Light;
        public static event Action Changed;

        public static void Set(ThemePalette p)
        {
            Current = p;
            var h = Changed; if (h != null) h();
        }

        // ---- 예전 이름(호환)
        public static Color BgDark { get { return Current.Bg; } }
        public static Color Panel { get { return Current.Card; } }
        public static Color PanelAlt { get { return Current.CardAlt; } }
        public static Color Field { get { return Current.Field; } }
        public static Color FieldBorder { get { return Current.FieldBorder; } }
        public static Color Border { get { return Current.Border; } }
        public static Color Gold { get { return Current.Accent; } }
        public static Color GoldBright { get { return Current.AccentHover; } }
        public static Color GoldDim { get { return Current.AccentPressed; } }
        public static Color TextPrimary { get { return Current.Text; } }
        public static Color TextMuted { get { return Current.TextMuted; } }
        public static Color Danger { get { return Current.Danger; } }
        public static Color OnGold { get { return Current.OnAccent; } }
        // ---- 새 이름
        public static Color Accent { get { return Current.Accent; } }
        public static Color Sidebar { get { return Current.Sidebar; } }
        public static Color Success { get { return Current.Success; } }
        public static Color Warn { get { return Current.Warn; } }
        public static Color Hover { get { return Current.Hover; } }

        private const string TagPrimary = "lux-primary";
        private const int ButtonRadius = 10;

        // ---- 글꼴 (캐시해서 GDI 핸들이 새지 않게 한다)
        private static readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private static string _family;

        public static string Family
        {
            get
            {
                if (_family != null) return _family;
                AppFont.Init();
                if (AppFont.Active) { _family = AppFont.Family; return _family; }
                string[] want = { "Segoe UI Variable Text", "Segoe UI", "맑은 고딕", "Malgun Gothic" };
                var installed = new HashSet<string>();
                try { foreach (var f in FontFamily.Families) installed.Add(f.Name); } catch { }
                foreach (var w in want) if (installed.Contains(w)) { _family = w; return _family; }
                _family = SystemFonts.DefaultFont.FontFamily.Name;
                return _family;
            }
        }

        public static Font Font(float size, FontStyle style)
        {
            if (AppFont.Active) style = FontStyle.Bold;      // 나눔고딕은 Bold 한 종류만 내장
            if (Dpi.EmulateFontScale) size = size * (float)Dpi.Scale;   // (검증 전용) Windows 에서는 DPI 에 따라 글꼴이 저절로 커진다
            string key = size.ToString("0.0") + "|" + (int)style;
            lock (_fonts)
            {
                Font f;
                if (!_fonts.TryGetValue(key, out f)) { f = new Font(Family, size, style, GraphicsUnit.Point); _fonts[key] = f; }
                return f;
            }
        }

        public static Font FontBase() { return Font(9.5f, FontStyle.Regular); }
        public static Font FontBold() { return Font(9.5f, FontStyle.Bold); }
        public static Font FontTitle() { return Font(14f, FontStyle.Bold); }
        public static Font FontSub() { return Font(9f, FontStyle.Regular); }
        public static Font FontMono(float size)
        {
            if (AppFont.Active) return Font(size, FontStyle.Bold);
            string key = "mono|" + size.ToString("0.0");
            lock (_fonts)
            {
                Font f;
                if (!_fonts.TryGetValue(key, out f))
                {
                    string fam = "Consolas";
                    try { bool has = false; foreach (var ff in FontFamily.Families) if (ff.Name == "Cascadia Mono") { has = true; break; } if (has) fam = "Cascadia Mono"; } catch { }
                    f = new Font(fam, size);
                    _fonts[key] = f;
                }
                return f;
            }
        }

        /// <summary>Segoe Fluent Icons(Win11) → Segoe MDL2 Assets(Win10) 순으로 아이콘 글꼴을 고른다.</summary>
        private static string _iconFamily;
        public static Font IconFont(float size)
        {
            if (_iconFamily == null)
            {
                _iconFamily = "";
                try
                {
                    foreach (var ff in FontFamily.Families)
                    {
                        if (ff.Name == "Segoe Fluent Icons") { _iconFamily = ff.Name; break; }
                        if (ff.Name == "Segoe MDL2 Assets") _iconFamily = ff.Name;
                    }
                }
                catch { }
            }
            string key = "icon|" + size.ToString("0.0");
            lock (_fonts)
            {
                Font f;
                if (!_fonts.TryGetValue(key, out f)) { f = new Font(_iconFamily.Length > 0 ? _iconFamily : Family, size); _fonts[key] = f; }
                return f;
            }
        }
        public static bool HasIconFont { get { IconFont(10); return _iconFamily.Length > 0; } }

        /// <summary>이 버튼을 강조(채움) 버튼으로 표시한다. Apply() 를 부르기 전에 호출해야 한다.</summary>
        public static void MarkPrimary(Button b) { b.Tag = TagPrimary; }
        private static bool IsPrimary(Button b) { return (b.Tag as string) == TagPrimary; }

        /// <summary>폼과 그 하위 컨트롤 전체에 현재 팔레트를 입힌다. 항상 불투명한 색만 사용한다.</summary>
        public static void Apply(Control root)
        {
            if (root is Form)
            {
                root.BackColor = Current.Bg;
                root.ForeColor = Current.Text;
                var f = (Form)root;
                if (f.IsHandleCreated) Dwm.Apply(f, Current);
                else
                {
                    f.HandleCreated -= OnHandleCreated;
                    f.HandleCreated += OnHandleCreated;
                }
            }
            ApplyChildren(root, root.BackColor);
        }

        private static void OnHandleCreated(object s, EventArgs e) { Dwm.Apply((Form)s, Current); }

        private static void ApplyChildren(Control parent, Color parentBg)
        {
            if(parent is SoftInput || parent is SoftNumber)return;
            foreach (Control c in parent.Controls)
            {
                Color childBg = StyleOne(c, parentBg);
                ApplyChildren(c, childBg);
            }
        }

        private static Color StyleOne(Control c, Color parentBg)
        {
            if (c is IThemed) { ((IThemed)c).ApplyTheme(); return c.BackColor; }
            if (c is Button)
            {
                var b = (Button)c;
                bool primary = IsPrimary(b);
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.UseVisualStyleBackColor = false;
                b.Cursor = Cursors.Hand;
                b.Font = FontBase();
                if (b.Height < Dpi.S(32)) b.Height = Dpi.S(32);
                if (primary)
                {
                    b.BackColor = Current.Accent; b.ForeColor = Current.OnAccent;
                    b.FlatAppearance.BorderColor = Current.Accent;
                    b.FlatAppearance.MouseOverBackColor = Current.AccentHover;
                    b.FlatAppearance.MouseDownBackColor = Current.AccentPressed;
                }
                else
                {
                    b.BackColor = Current.Card; b.ForeColor = Current.Text;
                    b.FlatAppearance.BorderColor = Current.FieldBorder;
                    b.FlatAppearance.MouseOverBackColor = Current.Hover;
                    b.FlatAppearance.MouseDownBackColor = Current.Selected;
                }
                RoundButton(b);
                return parentBg;
            }
            if (c is TextBoxBase)
            {
                c.BackColor = Current.Field; c.ForeColor = Current.Text;
                if (c is TextBox) ((TextBox)c).BorderStyle = c.Parent is FieldFrame ? BorderStyle.None : BorderStyle.FixedSingle;
                return c.BackColor;
            }
            if (c is NumericUpDown)
            {
                var n = (NumericUpDown)c;
                n.BorderStyle = BorderStyle.FixedSingle; n.BackColor = Current.Field; n.ForeColor = Current.Text;
                return n.BackColor;
            }
            if (c is ComboBox)
            {
                var cb = (ComboBox)c;
                cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Current.Field; cb.ForeColor = Current.Text;
                return cb.BackColor;
            }
            if (c is ListBox) { c.BackColor = Current.Field; c.ForeColor = Current.Text; return c.BackColor; }
            if (c is CheckBox) { c.ForeColor = Current.Text; c.BackColor = parentBg; return parentBg; }
            if (c is Label)
            {
                var l = (Label)c;
                string tag = l.Tag as string;
                if (tag == "t") l.ForeColor = Current.Text;
                else if (tag == "m") l.ForeColor = Current.TextMuted;
                else if (tag == "a") l.ForeColor = Current.Accent;
                else if (tag == "d") l.ForeColor = Current.Danger;
                else if (tag == "s") l.ForeColor = Current.Success;
                else
                if (l.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb() || l.ForeColor.ToArgb() == Color.Black.ToArgb()) l.ForeColor = Current.TextMuted;
                l.BackColor = parentBg;
                return parentBg;
            }
            if (c is DataGridView) { StyleGrid((DataGridView)c); return Current.Card; }
            if (c is TabControl) { StyleTabs((TabControl)c); return Current.Card; }
            if (c is TabPage) { c.BackColor = Current.Card; c.ForeColor = Current.Text; return Current.Card; }
            if (c is SplitContainer)
            {
                var sp = (SplitContainer)c;
                sp.BackColor = Current.Border; sp.Panel1.BackColor = Current.Card; sp.Panel2.BackColor = Current.Card;
                return Current.Card;
            }
            if (c is ProgressBar) return parentBg;

            c.BackColor = parentBg;
            if (c.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb()) c.ForeColor = Current.Text;
            return parentBg;
        }

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
            using (var path = RoundedRect(new Rectangle(0, 0, b.Width, b.Height), ButtonRadius)) b.Region = new Region(path);
            if (old != null) old.Dispose();
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>DataGridView 를 Windows 11 느낌(테두리 없음, 가로 구분선만, 넉넉한 행 높이)으로 꾸민다.</summary>
        public static void StyleGrid(DataGridView g)
        {
            var p = Current;
            g.BackgroundColor = p.Card;
            g.GridColor = p.Border;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible=false;
            g.RowHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
            g.RowHeadersDefaultCellStyle.BackColor = p.Card;
            g.RowHeadersDefaultCellStyle.ForeColor = p.TextMuted;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = p.Selected;
            g.ColumnHeadersDefaultCellStyle.BackColor = p.CardAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = p.TextMuted;
            g.ColumnHeadersDefaultCellStyle.Font = FontBold();
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.CardAlt;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Dpi.S(8), 0, 0, 0);
            g.ColumnHeadersHeight = Dpi.S(42);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.BackColor = p.Card;
            g.DefaultCellStyle.ForeColor = p.Text;
            g.DefaultCellStyle.Font = FontBase();
            g.DefaultCellStyle.Padding = new Padding(Dpi.S(8), 0, Dpi.S(4), 0);
            Color sel = p.Dark ? Color.FromArgb(52, 66, 78) : Color.FromArgb(221, 236, 250);
            g.DefaultCellStyle.SelectionBackColor = sel;
            g.DefaultCellStyle.SelectionForeColor = p.Text;
            g.AlternatingRowsDefaultCellStyle.BackColor = p.Card;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = sel;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = p.Text;
            g.RowTemplate.Height = Dpi.S(42);
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.AlternatingRowsDefaultCellStyle.BackColor=p.CardAlt;
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
            using (var bg = new SolidBrush(selected ? Panel : BgDark)) e.Graphics.FillRectangle(bg, rect);
            if (selected) using (var a = new SolidBrush(Accent)) e.Graphics.FillRectangle(a, rect.Left + 8, rect.Bottom - 3, rect.Width - 16, 3);
            TextRenderer.DrawText(e.Graphics, page.Text, selected ? FontBold() : FontBase(), rect,
                selected ? TextPrimary : TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>대화상자 상단에 타이틀/부제를 담은 헤더. 완전히 불투명한 색만 사용한다.</summary>
        public static Panel BuildHeader(string title, string subtitle)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Panel };
            var line = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Border };
            header.Controls.Add(line);
            header.Controls.Add(new Label { Text = title, Left = 20, Top = 10, AutoSize = true, Font = FontTitle(), ForeColor = TextPrimary, BackColor = Panel });
            header.Controls.Add(new Label { Text = subtitle, Left = 22, Top = 38, AutoSize = true, Font = FontSub(), ForeColor = TextMuted, BackColor = Panel });
            return header;
        }
    }

    /// <summary>Theme.Apply 가 직접 색을 입히지 않고 컨트롤 스스로 팔레트를 다시 읽게 하는 표시용 인터페이스.</summary>
    public interface IThemed { void ApplyTheme(); }
}
