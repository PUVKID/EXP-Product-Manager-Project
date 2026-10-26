using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>페이지가 공통으로 쓰는 작은 도우미들. Label 은 의미 태그(t/m/a/d/s)를 달아 테마가 바뀌면 색이 따라 바뀌게 한다.</summary>
    public static class Ui
    {
        private static double _scale;
        /// <summary>화면 배율(100%=1.0, 150%=1.5 ...). 시스템 DPI 기준으로 한 번만 계산한다. (Control.DeviceDpi 는 구형 .NET/Mono 에 없어서 쓰지 않는다)</summary>
        public static double Scale
        {
            get
            {
                if (_scale > 0) return _scale;
                try { using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) _scale = g.DpiX / 96.0; } catch { _scale = 1.0; }
                if (_scale < 1.0) _scale = 1.0;
                return _scale;
            }
        }
        /// <summary>96dpi 기준 픽셀을 현재 배율로 환산</summary>
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        public static Label Text(string text, float size = 9.5f, bool bold = false, string tone = "t")
        {
            return new Label
            {
                Text = text, AutoSize = true, Tag = tone,
                Font = Theme.Font(size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = tone == "m" ? Theme.TextMuted : Theme.TextPrimary, BackColor = Theme.BgDark
            };
        }

        public static Label Title(string text) { return Text(text, 20f, true); }
        public static Label Heading(string text) { return Text(text, 11.5f, true); }
        public static Label Muted(string text) { return Text(text, 9.5f, false, "m"); }

        public static FluentButton Button(string text, string glyph = "", BtnStyle style = BtnStyle.Standard, int width = 0)
        {
            var b = new FluentButton { Text = text, Glyph = glyph, Style = style };
            if (width > 0) b.Width = width;
            else
            {
                int w = TextRenderer.MeasureText(text, Theme.FontBase()).Width + 34 + (glyph.Length > 0 && Theme.HasIconFont ? 26 : 0);
                b.Width = Math.Max(84, w);
            }
            if (!Theme.HasIconFont) b.Glyph = "";
            b.Margin = new Padding(0, 0, 8, 0);
            return b;
        }

        public static ToggleSwitch Toggle(string text, bool on = false)
        {
            var t = new ToggleSwitch { Text = text, Checked = on };
            t.Width = TextRenderer.MeasureText(text, Theme.FontBase()).Width + 80;
            t.Margin = new Padding(0, 4, 0, 4);
            return t;
        }

        public static TextBox Input(int width, string text = "")
        {
            return new TextBox { Width = width, Text = text, Font = Theme.FontBase(), BorderStyle = BorderStyle.FixedSingle };
        }

        public static NumericUpDown Number(int min, int max, int width = 80)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Width = width, Font = Theme.FontBase() };
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
            Padding = new Padding(28, 20, 28, 20);
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
