using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
namespace BunjangAuto
{
    public class FieldFrame : Panel,IThemed
    {
        public FieldFrame() {DoubleBuffered=true;ApplyTheme();}
        protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);e.Control.GotFocus+=delegate{Invalidate();};e.Control.LostFocus+=delegate{Invalidate();};}
        public void ApplyTheme() {BackColor=Theme.Current.Field;Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e) {
            var p=Theme.Current;e.Graphics.Clear(Parent==null?p.Card:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var shape=Theme.RoundedRect(new Rectangle(0,0,Width-1,Height-1),Dpi.S(12)))using(var fill=new SolidBrush(p.Field))using(var pen=new Pen(ContainsFocus?p.Accent:p.FieldBorder)){e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);}
        }
    }
    // Shared, code-drawn artwork: no remote images or fonts are needed.
    public class HeroBanner : Control, IThemed
    {
        public string Eyebrow = "", Title = "", Subtitle = "", ActionText = "";
        bool hot;
        public HeroBanner() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop=false; Theme.Changed+=ApplyTheme;
        }
        public void ApplyTheme() { Invalidate(); }
        protected override void Dispose(bool disposing) { if(disposing) Theme.Changed-=ApplyTheme; base.Dispose(disposing); }
        protected override void OnMouseEnter(EventArgs e) { hot=true;Invalidate();base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot=false;Invalidate();base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys k) { return k==Keys.Enter || k==Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if(ActionText.Length>0 && (e.KeyCode==Keys.Enter || e.KeyCode==Keys.Space)){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e) {
            if(Width<2 || Height<2)return;
            var g=e.Graphics; var p=Theme.Current; int pad=Dpi.S(26);
            g.Clear(Parent==null?p.Bg:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new Rectangle(0,0,Width-1,Height-1);
            using(var shape=Theme.RoundedRect(r,Dpi.S(24))) {
                using(var fill=new LinearGradientBrush(r,p.Dark?Color.FromArgb(25,47,110):Color.FromArgb(32,76,195),p.Dark?Color.FromArgb(63,37,114):Color.FromArgb(105,71,203),18f))g.FillPath(fill,shape);
                var state=g.Save();g.SetClip(shape);
                // A quiet layered orbital motif leaves the reading area clear.
                int orbit=Height*2;
                using(var glow=new SolidBrush(Color.FromArgb(24,115,205,255)))g.FillEllipse(glow,Width-orbit/2,-orbit/2,orbit,orbit);
                using(var ring=new Pen(Color.FromArgb(36,220,238,255),Dpi.S(1))) {
                    for(int i=0;i<4;i++)g.DrawEllipse(ring,Width-Height-Dpi.S(12*i),-Height/2-Dpi.S(12*i),Height*2+Dpi.S(24*i),Height*2+Dpi.S(24*i));
                }
                g.Restore(state);
                using(var edge=new Pen(Color.FromArgb(48,255,255,255)))g.DrawPath(edge,shape);
            }
            bool action=ActionText.Length>0 && Width>Dpi.S(740);
            int textWidth=Width-pad*2-(action?Dpi.S(200):0);
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,Eyebrow,Theme.Font(8.5f,FontStyle.Bold),new Rectangle(pad,Dpi.S(17),textWidth,Dpi.S(22)),Color.FromArgb(205,220,255),flags);
            TextRenderer.DrawText(g,Title,Theme.Font(24f,FontStyle.Bold),new Rectangle(pad,Dpi.S(43),textWidth,Dpi.S(44)),Color.White,flags);
            TextRenderer.DrawText(g,Subtitle,Theme.Font(10f,FontStyle.Bold),new Rectangle(pad,Dpi.S(93),textWidth,Dpi.S(25)),Color.FromArgb(235,240,255),flags);
            if(action) {
                var cta=new Rectangle(Width-pad-Dpi.S(180),Height/2-Dpi.S(22),Dpi.S(180),Dpi.S(44));
                using(var shape=Theme.RoundedRect(cta,Dpi.S(14)))using(var fill=new SolidBrush(hot?Color.White:Color.FromArgb(233,241,255)))g.FillPath(fill,shape);
                TextRenderer.DrawText(g,ActionText,Theme.Font(10f,FontStyle.Bold),cta,Color.FromArgb(31,62,133),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }
            if(Focused&&ShowFocusCues)using(var pen=new Pen(Color.White,2))using(var shape=Theme.RoundedRect(new Rectangle(4,4,Width-9,Height-9),Dpi.S(20)))g.DrawPath(pen,shape);
        }
    }
    // Shared visual primitives for the customer app and the offline issuer.
    public class SoftCard : Panel, IThemed
    {
        public SoftCard() { DoubleBuffered = true; Padding = new Padding(24); Theme.Changed += Update; ApplyTheme(); }
        void Update() { if (!IsDisposed) { ApplyTheme(); Theme.Apply(this); Invalidate(true); } }
        public void ApplyTheme() { BackColor = Theme.Panel; ForeColor = Theme.TextPrimary; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.BgDark : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Dpi.S(18)))
            using (var fill = new SolidBrush(Theme.Panel)) using (var edge = new Pen(Theme.Border)) { e.Graphics.FillPath(fill, p); e.Graphics.DrawPath(edge, p); }
        }
        protected override void Dispose(bool disposing) { if (disposing) Theme.Changed -= Update; base.Dispose(disposing); }
    }
    public class SoftButton : Button, IThemed
    {
        public bool Primary; bool hot, down; float hover;
        readonly Timer motion=new Timer { Interval=16 };
        public SoftButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Height = 40; Cursor = Cursors.Hand; Font = Theme.FontBase(); motion.Tick+=delegate {float target=hot?1:0;hover+=(target-hover)*.3f;if(Math.Abs(target-hover)<.02f){hover=target;motion.Stop();}Invalidate();}; }
        protected override void Dispose(bool disposing) { if(disposing)motion.Dispose();base.Dispose(disposing); }
        public void ApplyTheme() { BackColor = Theme.Panel; ForeColor = Theme.TextPrimary; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hot = true; motion.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = down = false; motion.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current; e.Graphics.Clear(Parent == null ? p.Bg : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (down ? p.AccentPressed : Mix(p.Accent,p.AccentHover,hover)) : Mix(p.CardAlt,p.Hover,hover);
            using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Dpi.S(10)))
            using (var brush = new SolidBrush(Enabled ? fill : p.CardAlt)) using (var pen = new Pen(Primary ? fill : p.Border)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, !Enabled ? p.TextMuted : Primary ? p.OnAccent : p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4,4,Width-8,Height-8));
        }
        static Color Mix(Color a,Color b,float t) { return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); }
    }
    public class BrandMark : Control, IThemed
    {
        public BrandMark() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); Size = new Size(42,42); TabStop = false; }
        public void ApplyTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Parent == null ? Theme.Sidebar : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.RoundedRect(new Rectangle(0,0,Width-1,Height-1), Math.Max(5,Width/3))) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b,p);
            // Original E mark, drawn without external artwork.
            using (var pen = new Pen(Theme.Current.OnAccent, Math.Max(2,Width/13f))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen,Width*.29f,Height*.29f,Width*.70f,Height*.29f); g.DrawLine(pen,Width*.29f,Height*.50f,Width*.60f,Height*.50f); g.DrawLine(pen,Width*.29f,Height*.71f,Width*.70f,Height*.71f); }
        }
    }
    public class SoftCombo : ComboBox
    {
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if((m.Msg==0x000F || m.Msg==0x0317 || m.Msg==0x0318) && IsHandleCreated && Width>30 && DropDownStyle!=ComboBoxStyle.Simple)using(var g=m.Msg==0x000F?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)) {
                var p=Theme.Current;int w=Dpi.S(22);var r=new Rectangle(Width-w-1,1,w,Height-2);
                using(var brush=new SolidBrush(p.CardAlt))g.FillRectangle(brush,r);
                using(var pen=new Pen(p.FieldBorder))g.DrawRectangle(pen,0,0,Width-1,Height-1);
                g.SmoothingMode=SmoothingMode.AntiAlias;float x=r.Left+r.Width/2f,y=Height/2f;
                using(var pen=new Pen(Enabled?p.TextMuted:p.Border,Dpi.S(2))) {g.DrawLine(pen,x-Dpi.S(3),y-Dpi.S(1),x,y+Dpi.S(2));g.DrawLine(pen,x,y+Dpi.S(2),x+Dpi.S(3),y-Dpi.S(1));}
            }
        }
        public SoftCombo() { DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=30;DropDownStyle=ComboBoxStyle.DropDownList;Font=Theme.FontBase(); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var b=new SolidBrush(selected?Theme.Current.Selected:Theme.Current.Field))e.Graphics.FillRectangle(b,e.Bounds);
            string text=e.Index>=0?Convert.ToString(Items[e.Index]):Text;
            TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Enabled?Theme.TextPrimary:Theme.TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
        }
    }
    public static class Design
    {
        public static Label Label(string text, int x, int y, int w, int h, float size, string tone)
        { return new Label { Text = text, Left = x, Top = y, Width = w, Height = h, Font = Theme.Font(size, FontStyle.Bold), Tag = tone, ForeColor = tone == "m" ? Theme.TextMuted : Theme.TextPrimary }; }
        public static SoftButton Button(string text, int x, int y, int w, bool primary)
        { return new SoftButton { Text = text, Left = x, Top = y, Width = w, Primary = primary }; }
        public static TextBox Input(string name, int x, int y, int w, int h, bool readOnly)
        { return new TextBox { Name = name, Left = x, Top = y, Width = w, Height = h, Font = Theme.FontBase(), Multiline = true, ReadOnly = readOnly, ScrollBars = ScrollBars.None, MaxLength = Lumivolt.Security.LicenseProtocol.MaxText, BorderStyle = BorderStyle.FixedSingle }; }
        public static void Scale(Form form)
        {
            form.AutoScaleMode = AutoScaleMode.None;
            if (Dpi.Scale > 1.001) form.Scale(new SizeF((float)Dpi.Scale,(float)Dpi.Scale));
            var area=Screen.FromControl(form).WorkingArea;
            if(form.Height>area.Height-32 || form.Width>area.Width-32) {
                var content=form.ClientSize; form.AutoScroll=true; form.AutoScrollMinSize=content;
                form.Size=new Size(Math.Min(form.Width,area.Width-32),Math.Min(form.Height,area.Height-32));
            }
        }
        public static bool AdminDark
        {
            get { try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Lumivolt\Issuer")) return k != null && Convert.ToString(k.GetValue("Theme", "Light")) == "Dark"; } catch { return false; } }
            set { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Lumivolt\Issuer")) k.SetValue("Theme", value ? "Dark" : "Light"); }
        }
    }
}
