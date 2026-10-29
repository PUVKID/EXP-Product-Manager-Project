using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
namespace BunjangAuto
{
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
        public bool Primary; bool hot, down;
        public SoftButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Height = 40; Cursor = Cursors.Hand; Font = Theme.FontBase(); }
        public void ApplyTheme() { BackColor = Theme.Panel; ForeColor = Theme.TextPrimary; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current; e.Graphics.Clear(Parent == null ? p.Bg : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (down ? p.AccentPressed : hot ? p.AccentHover : p.Accent) : hot ? p.Hover : p.CardAlt;
            using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Dpi.S(10)))
            using (var brush = new SolidBrush(Enabled ? fill : p.CardAlt)) using (var pen = new Pen(Primary ? fill : p.Border)) { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, !Enabled ? p.TextMuted : Primary ? p.OnAccent : p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4,4,Width-8,Height-8));
        }
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
