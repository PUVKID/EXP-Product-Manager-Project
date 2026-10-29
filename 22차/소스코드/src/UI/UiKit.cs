using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>페이지가 공통으로 쓰는 작은 도우미들. Label 은 의미 태그(t/m/a/d/s)를 달아 테마가 바뀌면 색이 따라 바뀌게 한다.</summary>
    public static class Ui
    {
        public static double Scale { get { return Dpi.Scale; } }
        public static double ForcedScale { get { return Dpi.ForcedScale; } set { Dpi.ForcedScale = value; } }
        public static bool EmulateFontScale { get { return Dpi.EmulateFontScale; } set { Dpi.EmulateFontScale = value; } }
        /// <summary>96dpi 기준 픽셀을 현재 배율로 환산</summary>
        public static int S(int px) { return Dpi.S(px); }

        /// <summary>
        /// 화면을 96dpi 기준 숫자(위치·크기)로 설계해 두고, 만든 직후 한 번 현재 배율로 키운다.
        /// WinForms 자동 배율(AutoScaleMode)은 "나중에 만든" 컨트롤에는 적용되지 않아서 직접 처리한다.
        /// 스스로 실제 픽셀로 크기를 정하는 컨트롤(IPreScaled)은 위치만 키운다.
        /// </summary>
        public static void ScaleTree(Control root)
        {
            var responsive=root as IDpiLayout;
            if(responsive!=null){responsive.LayoutForDpi();return;}
            double f = Scale;
            if (f <= 1.001) return;
            root.Padding = ScalePad(root.Padding, f);
            root.MinimumSize = new Size((int)Math.Round(root.MinimumSize.Width * f), (int)Math.Round(root.MinimumSize.Height * f));
            ScaleChildren(root, f);
        }

        private static Padding ScalePad(Padding p, double f) { return new Padding((int)Math.Round(p.Left * f), (int)Math.Round(p.Top * f), (int)Math.Round(p.Right * f), (int)Math.Round(p.Bottom * f)); }

        private static void ScaleChildren(Control parent, double f)
        {
            foreach (Control k in parent.Controls)
            {
                bool pre = k is IPreScaled;
                bool fill = k.Dock == DockStyle.Fill;
                int x = k.Left, y = k.Top, w = k.Width, h = k.Height;
                switch (k.Dock)
                {
                    case DockStyle.None: x = R(x, f); y = R(y, f); if (!pre) { w = R(w, f); h = R(h, f); } break;
                    case DockStyle.Top: case DockStyle.Bottom: if (!pre) h = R(h, f); break;
                    case DockStyle.Left: case DockStyle.Right: if (!pre) w = R(w, f); break;
                }
                bool autoSize = (k is Label && ((Label)k).AutoSize) || (k is FlowLayoutPanel && ((FlowLayoutPanel)k).AutoSize);
                if (k.Dock == DockStyle.None) { if (autoSize) k.Location = new Point(x, y); else k.SetBounds(x, y, w, h); }
                else if (!fill && !autoSize) k.Size = new Size(w, h);
                k.Margin = ScalePad(k.Margin, f);
                if (!(k is IPreScaled)) k.Padding = ScalePad(k.Padding, f);
                if (k is DataGridView) ScaleGrid((DataGridView)k, f);
                if (k.MinimumSize != Size.Empty) k.MinimumSize = new Size(R(k.MinimumSize.Width, f), R(k.MinimumSize.Height, f));
                if(!(k is SoftInput) && !(k is SoftNumber))ScaleChildren(k, f);
            }
        }

        private static void ScaleGrid(DataGridView g, double f)
        {
            foreach (DataGridViewColumn c in g.Columns) { c.Width = R(c.Width, f); if (c.MinimumWidth > 5) c.MinimumWidth = R(c.MinimumWidth, f); }
            g.RowHeadersWidth = Math.Max(4, R(g.RowHeadersWidth, f));
        }

        private static int R(int v, double f) { return (int)Math.Round(v * f); }

        /// <summary>대화상자(Form)를 만든 뒤(컨트롤을 다 붙인 다음) 호출: 내용과 창 크기를 현재 배율로 키운다.</summary>
        public static void ScaleForm(Form f)
        {
            f.AutoScaleMode = AutoScaleMode.None;
            double s = Scale;
            if (s <= 1.001) return;
            ScaleChildren(f, s);
            f.ClientSize = new Size(R(f.ClientSize.Width, s), R(f.ClientSize.Height, s));
        }

        public static Label Text(string text, float size = 9.5f, bool bold = false, string tone = "t")
        {
            return new Label
            {
                Text = text, AutoSize = true, Tag = tone,
                Font = Theme.Font(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = tone == "m" ? Theme.TextMuted : Theme.TextPrimary, BackColor = Theme.BgDark
            };
        }

        public static Label Title(string text) { return Text(text, 24f, true); }
        public static Label Heading(string text) { return Text(text, 11.5f, true); }
        public static Label Muted(string text) { return Text(text, 9.5f, false, "m"); }

        public static FluentButton Button(string text, string glyph = "", BtnStyle style = BtnStyle.Standard, int width = 0)
        {
            var b = new FluentButton { Text = text, Glyph = glyph, Style = style };
            // FluentButton 은 실제 픽셀로 크기를 정한다(IPreScaled): 글자 폭(이미 화면 픽셀) + 여백만 배율 적용
            if (width > 0) b.Width = S(width);
            else
            {
                int w = TextRenderer.MeasureText(text, Theme.FontBase()).Width + S(34) + (glyph.Length > 0 && Theme.HasIconFont ? S(26) : 0);
                b.Width = Math.Max(S(84), w);
            }
            if (!Theme.HasIconFont) b.Glyph = "";
            b.Margin = new Padding(S(0), 0, S(8), 0);
            return b;
        }

        public static ToggleSwitch Toggle(string text, bool on = false)
        {
            var t = new ToggleSwitch { Text = text, Checked = on };
            t.Width = TextRenderer.MeasureText(text, Theme.FontBase()).Width + S(80);
            t.Margin = new Padding(0, 4, 0, 4);
            return t;
        }

        public static SoftInput Input(int width, string text = "")
        {
            return new SoftInput { Width = width, Text = text, Font = Theme.FontBase() };
        }

        public static SoftNumber Number(int min, int max, int width = 80)
        {
            return new SoftNumber { Minimum = min, Maximum = max, Width = Math.Max(144,width), Font = Theme.FontBase() };
        }

        public static FlowLayoutPanel Row(params Control[] items)
        {
            var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0), BackColor = Theme.BgDark };
            f.Controls.AddRange(items);
            return f;
        }

        public static void OpenFolder(string path)
        {
            try { System.IO.Directory.CreateDirectory(path); System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\""); }
            catch (Exception ex) { FluentBox.Show("폴더를 열 수 없습니다: " + ex.Message, "오류"); }
        }

        /// <summary>백그라운드에서 일을 하고 결과를 UI 스레드에서 받는다(서버 호출 등으로 화면이 멈추지 않게).</summary>
        public static void Background<T>(Control ui, Func<T> work, Action<T> done)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                T r = default(T);
                try { r = work(); } catch (Exception ex) { Log.Write("WARN", "Admin", "admin.error", "관리자 요청 실패: " + ex.Message, "FAIL"); }
                try { if (!ui.IsDisposed && ui.IsHandleCreated) ui.BeginInvoke((Action)delegate { done(r); }); } catch { }
            });
        }
    }

    /// <summary>스스로 실제 화면 픽셀로 크기를 정하는 컨트롤 표시(Ui.ScaleTree 가 크기를 다시 키우지 않는다).</summary>
    public interface IPreScaled { }
    public interface IDpiLayout { void LayoutForDpi(); }

    /// <summary>각 화면의 공통 틀: 제목 줄 + 스크롤 가능한 본문. OnActivated 는 화면이 보일 때마다 호출된다.</summary>
    public abstract class PageBase : Panel
    {
        public string Key = "";
        public string PageTitle = "";
        protected readonly AppHost Host;

        protected PageBase(AppHost host)
        {
            Host = host;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            Padding = new Padding(32, 26, 32, 26);
            BackColor = Theme.BgDark;
            DoubleBuffered = true;
        }

        /// <summary>손으로 Location 을 준 자식 컨트롤은 Padding 만큼 안쪽으로 밀어 준다(Dock 된 자식은 Padding 이 알아서 적용됨).</summary>
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control.Dock == DockStyle.None) { e.Control.Left += Padding.Left; e.Control.Top += Padding.Top; }
        }

        public virtual void OnActivated() { }
        public virtual void OnDeactivated() { }
        /// <summary>팔레트가 바뀐 뒤 페이지 고유의 추가 작업(그리드 재스타일 등)</summary>
        public virtual void OnThemeChanged() { }
    }
}
