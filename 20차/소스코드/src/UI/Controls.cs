using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>모든 커스텀 컨트롤의 공통 기반: 더블 버퍼링, 팔레트 변경 시 자동 다시 그리기.</summary>
    public abstract class FluentControl : Control, IThemed
    {
        private readonly Timer motion = new Timer { Interval = 16 };
        private double motionTarget;
        private readonly VisibilityWatch visibility;
        protected double MotionValue;
        public bool AnimationRunning { get { return motion.Enabled; } }
        protected void AnimateTo(double target)
        {
            motionTarget=target;
            if(!Visible || !IsHandleCreated || MotionPreferences.ReducedMotion || (FindForm()!=null&&FindForm().WindowState==FormWindowState.Minimized)){MotionValue=target;Invalidate();return;}
            if(Math.Abs(MotionValue-target)>.001)motion.Start();
        }
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible){motion.Stop();MotionValue=motionTarget;}}
        protected FluentControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            Font = Theme.FontBase();
            Theme.Changed += OnThemeChanged;
            MotionPreferences.Changed+=OnMotionChanged;
            motion.Tick+=delegate{MotionValue+=(motionTarget-MotionValue)*.32;if(Math.Abs(motionTarget-MotionValue)<.002){MotionValue=motionTarget;motion.Stop();}Invalidate();};
            visibility=new VisibilityWatch(this,delegate(bool visible){if(!visible){motion.Stop();MotionValue=motionTarget;}});
        }
        private void OnMotionChanged(){if(MotionPreferences.ReducedMotion){motion.Stop();MotionValue=motionTarget;Invalidate();}}
        private void OnThemeChanged() { if (!IsDisposed) { ApplyTheme(); Invalidate(); } }
        public virtual void ApplyTheme() { }
        protected override void Dispose(bool disposing) { if (disposing) {Theme.Changed -= OnThemeChanged;MotionPreferences.Changed-=OnMotionChanged;visibility.Dispose();motion.Dispose();} base.Dispose(disposing); }

        /// <summary>둥근 모서리 바깥을 칠할 색: 부모의 실제 배경색.</summary>
        protected Color ParentBg { get { return Parent != null ? Parent.BackColor : Theme.Current.Bg; } }

        /// <summary>96dpi 기준 값을 현재 화면 배율로 환산(그리기 안쪽의 고정 픽셀용)</summary>
        protected static int S(int px) { return Ui.S(px); }

        protected static void Smooth(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; }

        protected static readonly TextFormatFlags LeftFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
    }

    /// <summary>둥근 모서리 + 얇은 테두리의 카드(바탕보다 한 층 위).</summary>
    public class CardPanel : Panel, IThemed
    {
        public int Radius = 18;
        public bool Tinted;
        public bool Raised = true;     // false 면 카드색 없이 바탕색 위에 테두리만
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(16);
            Theme.Changed += OnThemeChanged;
            ApplyTheme();
        }
        private void OnThemeChanged() { if (!IsDisposed) { ApplyTheme(); ApplyToChildren(this); Invalidate(); } }
        public void ApplyTheme() { BackColor = Tinted ? FluentButton.Blend(Theme.Accent,Theme.Current.Card,.92) : Theme.Current.Card; ForeColor = Theme.Current.Text; }
        protected override void Dispose(bool disposing) { if (disposing) Theme.Changed -= OnThemeChanged; base.Dispose(disposing); }

        /// <summary>카드 안의 일반 컨트롤(Label 등)도 카드색을 따르도록 다시 칠한다.</summary>
        public static void ApplyToChildren(Control root) { foreach (Control c in root.Controls) { Theme.Apply(c); } }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color outer = Parent != null ? Parent.BackColor : Theme.Current.Bg;
            g.Clear(outer);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(r, Radius))
            using (var fill = new SolidBrush(Tinted ? FluentButton.Blend(Theme.Accent,Theme.Current.Card,.92) : Theme.Current.Card))
            using (var pen = new Pen(Theme.Current.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    public enum BtnStyle { Standard, Primary, Subtle, Danger }

    public class FluentButton : FluentControl, IPreScaled
    {
        public BtnStyle Style = BtnStyle.Standard;
        public string Glyph = "";
        private bool _hot, _down;
        public FluentButton() { Height = S(34); Width = S(110); Cursor = Cursors.Hand; }
        /// <summary>마우스로 누른 것과 같은 효과(키보드·자동화 테스트용)</summary>
        public void PerformClick() { OnClick(EventArgs.Empty); }

        [Browsable(true)] public override string Text { get { return base.Text; } set { base.Text = value; Invalidate(); } }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { _hot = true; AnimateTo(1); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = false; _down = false; AnimateTo(0); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnClick(EventArgs e) { if (Enabled) base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(ParentBg); Smooth(g);
            Color fill, border, fore;
            switch (Style)
            {
                case BtnStyle.Primary:
                    fill = _down ? p.AccentPressed : Blend(p.Accent,p.AccentHover,MotionValue); border = fill; fore = p.OnAccent; break;
                case BtnStyle.Danger:
                    fill = _down ? Blend(p.Danger, p.Card, .7) : _hot ? Blend(p.Danger, p.Card, .85) : p.Card; border = p.Danger; fore = p.Danger; break;
                case BtnStyle.Subtle:
                    fill = _down ? p.Selected : Blend(ParentBg,p.Hover,MotionValue); border = fill; fore = p.Text; break;
                default:
                    fill = _down ? p.Selected : Blend(p.Card,p.Hover,MotionValue); border = p.FieldBorder; fore = p.Text; break;
            }
            if (!Enabled) { fill = Style == BtnStyle.Primary ? Blend(p.Accent, ParentBg, .4) : p.CardAlt; border = p.Border; fore = Style == BtnStyle.Primary ? p.OnAccent : p.TextMuted; }
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(r, S(10)))
            using (var b = new SolidBrush(fill)) using (var pen = new Pen(border)) { g.FillPath(b, path); g.DrawPath(pen, path); }
            if (Focused && ShowFocusCues) using (var pen = new Pen(p.Accent, 2)) using (var path = Theme.RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), 4)) g.DrawPath(pen, path);

            int x = 0; var tr = new Rectangle(0, 0, Width, Height);
            if (Glyph.Length > 0)
            {
                var gf = Theme.IconFont(11f);
                var gs = TextRenderer.MeasureText(g, Glyph, gf, new Size(S(100), S(40)), TextFormatFlags.NoPadding);
                var ts = TextRenderer.MeasureText(g, Text, Font, new Size(1000, 40), TextFormatFlags.NoPadding);
                int total = gs.Width + S(8) + (Text.Length > 0 ? ts.Width : 0);
                x = (Width - total) / 2;
                TextRenderer.DrawText(g, Glyph, gf, new Rectangle(x, 0, gs.Width + S(2), Height), fore, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                tr = new Rectangle(x + gs.Width + S(8), 0, Width - x - gs.Width - S(8), Height);
                TextRenderer.DrawText(g, Text, Font, tr, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else TextRenderer.DrawText(g, Text, Font, tr, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        public static Color Blend(Color a, Color b, double t) { return Color.FromArgb((int)(a.R * (1 - t) + b.R * t), (int)(a.G * (1 - t) + b.G * t), (int)(a.B * (1 - t) + b.B * t)); }
    }

    /// <summary>사이드바 항목: 아이콘 + 글자, 선택 시 왼쪽에 강조 막대.</summary>
    public class NavItem : FluentControl, IPreScaled
    {
        public string Glyph = "";
        public string Key = "";
        private bool _sel, _hot;
        public bool Selected { get { return _sel; } set { _sel = value; AnimateTo(value?1:0); Invalidate(); } }
        public NavItem() { Height = S(40); Cursor = Cursors.Hand; TabStop = true; AccessibleRole=AccessibleRole.PageTab; }
        public override string Text {get{return base.Text;}set{base.Text=value;AccessibleName=value;}}
        protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(p.Sidebar); Smooth(g);
            var r = new Rectangle(S(12), S(2), Width - S(24), Height - S(4));
            using (var path = Theme.RoundedRect(r, S(10))) using (var b = new SolidBrush(FluentButton.Blend(_hot?p.Hover:p.Sidebar,p.Selected,MotionValue))) g.FillPath(b, path);
            if(_sel)using(var indicator=new SolidBrush(p.Accent))g.FillRectangle(indicator,S(12),S(10),S(3),Height-S(20));

            if (Focused && ShowFocusCues) using (var pen = new Pen(p.Accent)) using (var path = Theme.RoundedRect(r, 8)) g.DrawPath(pen, path);
            Color fore = p.Text;
            int tx = S(20);
            if (Theme.HasIconFont && Glyph.Length > 0)
            {
                TextRenderer.DrawText(g, Glyph, Theme.IconFont(12f), new Rectangle(Width<=S(100)?(Width-S(28))/2:S(18), 0, S(28), Height), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                tx = S(54);
            }
            if(Width>S(100))TextRenderer.DrawText(g, Text, _sel ? Theme.Font(9.5f, FontStyle.Bold) : Font, new Rectangle(tx, 0, Width - tx - S(8), Height), p.Text, LeftFlags);
        }
    }

    /// <summary>Windows 11 스타일 켬/끔 스위치(글자 포함).</summary>
    public class ToggleSwitch : FluentControl, IPreScaled
    {
        private bool _checked;
        private bool _hot;
        public event EventHandler CheckedChanged;
        public ToggleSwitch() { Height = S(28); Width = S(360); Cursor = Cursors.Hand; }
        public bool Checked { get { return _checked; } set { if (_checked == value) return; _checked = value; Invalidate(); var h = CheckedChanged; if (h != null) h(this, EventArgs.Empty); } }
        public override string Text { get { return base.Text; } set { base.Text = value; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { if (Enabled) { Checked = !Checked; Focus(); } base.OnClick(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(ParentBg); Smooth(g);
            int w = S(40), h = S(20), y = (Height - h) / 2;
            var track = new Rectangle(1, y, w, h);
            Color on = Enabled ? p.Accent : FluentButton.Blend(p.Accent, ParentBg, .5);
            using (var path = Theme.RoundedRect(track, h / 2))
            {
                if (_checked) using (var b = new SolidBrush(on)) g.FillPath(b, path);
                else { using (var b = new SolidBrush(_hot ? p.Hover : ParentBg)) g.FillPath(b, path); using (var pen = new Pen(p.TextMuted)) g.DrawPath(pen, path); }
            }
            int d = _checked ? S(14) : S(12);
            int cx = _checked ? track.Right - d - S(4) : track.Left + S(4);
            using (var b = new SolidBrush(_checked ? p.OnAccent : p.TextMuted)) g.FillEllipse(b, cx, y + (h - d) / 2, d, d);
            if (Focused && ShowFocusCues) using (var pen = new Pen(p.Accent, 2)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(w + S(14), 0, Width - w - S(14), Height), Enabled ? p.Text : p.TextMuted, LeftFlags);
        }
    }

    /// <summary>라이트 | 다크 | 시스템 같은 상호 배타적 선택 막대.</summary>
    public class Segmented : FluentControl, IPreScaled
    {
        public List<string> Items = new List<string>();
        private int _idx; private int _hot = -1;
        public event EventHandler SelectedIndexChanged;
        public Segmented() { Height = S(32); Width = S(260); Cursor = Cursors.Hand; TabStop=true; AccessibleRole=AccessibleRole.List; }
        public int SelectedIndex { get { return _idx; } set { if (_idx == value) return; _idx = value; Invalidate(); var h = SelectedIndexChanged; if (h != null) h(this, EventArgs.Empty); } }
        public void SetIndexSilently(int i) { _idx = i; Invalidate(); }
        private int Hit(Point pt) { if (Items.Count == 0) return -1; int i = pt.X * Items.Count / Math.Max(1, Width); return i < 0 || i >= Items.Count ? -1 : i; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != _hot) { _hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { if(Enabled){int i = Hit(e.Location); if (i >= 0) SelectedIndex = i; Focus();} base.OnMouseClick(e); }
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){if(Enabled&&Items.Count>0){int next=SelectedIndex;if(e.KeyCode==Keys.Left)next=Math.Max(0,next-1);else if(e.KeyCode==Keys.Right)next=Math.Min(Items.Count-1,next+1);else if(e.KeyCode==Keys.Home)next=0;else if(e.KeyCode==Keys.End)next=Items.Count-1;else{base.OnKeyDown(e);return;}SelectedIndex=next;e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(ParentBg); Smooth(g);
            var outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(outer, 10)) { using (var b = new SolidBrush(p.CardAlt)) g.FillPath(b, path); using (var pen = new Pen(p.Border)) g.DrawPath(pen, path); }
            if (Items.Count == 0) return;
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6),p.Text,p.CardAlt);
            int w = (Width - 4) / Items.Count;
            for (int i = 0; i < Items.Count; i++)
            {
                var r = new Rectangle(2 + i * w, 2, w, Height - 5);
                if (i == _idx) { using (var path = Theme.RoundedRect(r, 5)) { using (var b = new SolidBrush(p.Card)) g.FillPath(b, path); using (var pen = new Pen(p.FieldBorder)) g.DrawPath(pen, path); } }
                else if (i == _hot) using (var path = Theme.RoundedRect(r, 5)) using (var b = new SolidBrush(p.Hover)) g.FillPath(b, path);
                TextRenderer.DrawText(g, Items[i], i == _idx ? Theme.Font(9.5f, FontStyle.Bold) : Font, r, i == _idx ? p.Accent : p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    public class FluentProgress : FluentControl, IPreScaled
    {
        private int _v, _max = 100;
        public bool Indeterminate;
        public FluentProgress() { Height = S(6); Width = S(300); TabStop = false; }
        public int Maximum { get { return _max; } set { _max = Math.Max(1, value); AnimateTo((double)_v/_max); Invalidate(); } }
        public int Value { get { return _v; } set { _v = Math.Max(0, Math.Min(value, _max)); AnimateTo((double)_v/_max); Invalidate(); } }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(ParentBg); Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(r, Height / 2)) using (var b = new SolidBrush(p.Border)) g.FillPath(b, path);
            int w = (int)((Width - 1) * Math.Max(0,Math.Min(1,MotionValue)));
            if (w > 0) using (var path = Theme.RoundedRect(new Rectangle(0, 0, Math.Max(w, Height), Height - 1), Height / 2)) using (var b = new SolidBrush(p.Accent)) g.FillPath(b, path);
        }
    }

    /// <summary>실제 현재 실행의 완료/전체 수를 큰 숫자로 표시합니다.</summary>
    public class RunFraction : FluentControl
    {
        private int done,total;
        public int Done {get{return done;}}
        public int Total {get{return total;}}
        public string DisplayText {get{return done+" / "+total;}}
        public void SetProgress(int current,int maximum){done=Math.Max(0,current);total=Math.Max(0,maximum);AccessibleName="등록 진행 "+DisplayText;Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(ParentBg);var p=Theme.Current;string left=done.ToString(),right="/ "+total;
            float size=56f;Font big,small;int first,second;
            do{big=Theme.Font(size,FontStyle.Bold);small=Theme.Font(size*.62f,FontStyle.Bold);first=TextRenderer.MeasureText(g,left,big,Size.Empty,TextFormatFlags.NoPadding).Width;second=TextRenderer.MeasureText(g,right,small,Size.Empty,TextFormatFlags.NoPadding).Width;if(first+second+S(16)<=Width||size<=12f)break;size-=1f;}while(true);
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,left,big,new Rectangle(0,0,first+S(2),Height),p.Text,flags);
            TextRenderer.DrawText(g,right,small,new Rectangle(first+S(12),S(12),Math.Max(1,Width-first-S(12)),Height-S(12)),p.TextMuted,flags);
        }
    }

    /// <summary>진행률을 네이티브 벡터로 그리며 공통 가시성·Dispose 수명 관리를 사용합니다.</summary>
    public class CircularProgress : FluentControl, IPreScaled
    {
        private int value,maximum=1;
        public CircularProgress(){Size=new Size(S(164),S(164));TabStop=false;AccessibleRole=AccessibleRole.ProgressBar;}
        public int Maximum {get{return maximum;}set{maximum=Math.Max(1,value);AnimateTo((double)this.value/maximum);Invalidate();}}
        public int Value {get{return value;}set{this.value=Math.Max(0,Math.Min(value,maximum));AnimateTo((double)this.value/maximum);AccessibleName="등록 진행 "+Percentage+"%";Invalidate();}}
        public int Percentage {get{return (int)Math.Round(value*100.0/maximum);}}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(ParentBg);Smooth(g);int line=S(12),edge=line/2+S(2),diameter=Math.Min(Width,Height)-edge*2;
            if(diameter<2)return;var circle=new Rectangle((Width-diameter)/2,(Height-diameter)/2,diameter,diameter);
            using(var track=new Pen(Theme.Current.Border,line))g.DrawEllipse(track,circle);
            float sweep=(float)(360*Math.Max(0,Math.Min(1,MotionValue)));
            if(sweep>.01f)using(var arc=new Pen(Theme.Accent,line)){arc.StartCap=arc.EndCap=LineCap.Round;g.DrawArc(arc,circle,-90,sweep);}
            TextRenderer.DrawText(g,Percentage+"%",Theme.Font(20f,FontStyle.Bold),ClientRectangle,Theme.TextPrimary,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
        }
    }

    /// <summary>작은 알약 모양 상태 표시(라이선스 상태, 로그인 상태 등).</summary>
    public class Chip : FluentControl, IPreScaled
    {
        public Color? Tone;     // null 이면 팔레트의 TextMuted
        public Chip() { Height = S(26); TabStop = false; }
        public override string Text { get { return base.Text; } set { base.Text = value; Width = TextRenderer.MeasureText(value ?? "", Theme.Font(9f, FontStyle.Regular)).Width + S(28); Invalidate(); } }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current; g.Clear(ParentBg); Smooth(g);
            Color tone = Tone ?? p.TextMuted;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(r, Height / 2))
            { using (var b = new SolidBrush(FluentButton.Blend(tone, p.Card, p.Dark ? .8 : .88))) g.FillPath(b, path); using (var pen = new Pen(FluentButton.Blend(tone, p.Card, .55))) g.DrawPath(pen, path); }
            using (var b = new SolidBrush(tone)) g.FillEllipse(b, S(9), Height / 2 - S(3), S(6), S(6));
            TextRenderer.DrawText(g, Text, Theme.Font(9f, FontStyle.Regular), new Rectangle(S(20), 0, Width - S(22), Height), p.Text, LeftFlags);
        }
    }

    /// <summary>Dashboard 의 숫자 카드.</summary>
    public class StatCard : CardPanel
    {
        public string Caption = "", Value = "-", Hint = "";
        public Func<Color> Tone;
        public StatCard() { Height = 136; Padding = new Padding(18, 14, 18, 14); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var p = Theme.Current;
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,Caption,Theme.Font(9.5f,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(14),Width-Ui.S(36),Ui.S(24)),p.TextMuted,flags);
            float size=22f;
            while(size>14f && TextRenderer.MeasureText(g,Value,Theme.Font(size,FontStyle.Bold)).Width>Width-Ui.S(36))size-=1f;
            TextRenderer.DrawText(g,Value,Theme.Font(size,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(44),Width-Ui.S(36),Ui.S(40)),Tone!=null?Tone():p.Text,flags);
            if(Hint.Length>0)TextRenderer.DrawText(g,Hint,Theme.Font(9f,FontStyle.Bold),new Rectangle(Ui.S(18),Ui.S(84),Width-Ui.S(36),Ui.S(22)),p.TextMuted,flags);
        }
    }

    /// <summary>세로 막대 그래프(관리자 통계/대시보드 미니 차트). 외부 라이브러리 없이 직접 그린다.</summary>
    public class BarChart : FluentControl
    {
        public List<string> Labels = new List<string>();
        public List<double[]> Series = new List<double[]>();   // Series[k][i]
        public List<Func<Color>> Colors = new List<Func<Color>>();
        public BarChart() { Height = 180; TabStop = false; }
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current; g.Clear(p.Card); Smooth(g);
            int L = S(34), B = S(22), T = S(6), W = Width - L - S(6), H = Height - B - T;
            double max = 1; foreach (var s in Series) foreach (var v in s) max = Math.Max(max, v);
            using (var pen = new Pen(p.Border))
                for (int k = 0; k <= 2; k++)
                {
                    int y = T + (int)(H * (1 - k / 2.0)); g.DrawLine(pen, L, y, Width - 4, y);
                    TextRenderer.DrawText(g, ((int)Math.Round(max * k / 2.0)).ToString(), Theme.Font(8f, FontStyle.Regular), new Rectangle(0, y - S(8), L - S(4), S(16)), p.TextMuted, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                }
            int n = Labels.Count; if (n == 0) return;
            double gw = (double)W / n; int ns = Math.Max(1, Series.Count); double bw = Math.Max(S(3), gw / (ns + 1));
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < Series.Count; k++)
                {
                    double v = i < Series[k].Length ? Series[k][i] : 0; int bh = (int)(H * v / max);
                    var c = k < Colors.Count ? Colors[k]() : p.Accent;
                    using (var b = new SolidBrush(c)) g.FillRectangle(b, (float)(L + i * gw + k * bw + bw / 2), T + H - bh, (float)bw - 1, bh);
                }
                TextRenderer.DrawText(g, Labels[i], Theme.Font(8f, FontStyle.Regular), new Rectangle((int)(L + i * gw), Height - B + S(4), (int)gw, S(16)), p.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>카드 위에 놓는 목록 상자: 입력칸 색이 아니라 카드 색을 따른다.</summary>
    public class FlatList : ListBox, IThemed
    {
        public FlatList() { BorderStyle = BorderStyle.None; IntegralHeight = false; ApplyTheme(); }
        public void ApplyTheme() { BackColor = Theme.Current.Card; ForeColor = Theme.Current.Text; }
    }
}
