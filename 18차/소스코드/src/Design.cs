using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
namespace BunjangAuto
{
    /// <summary>상위 창과 패널의 가시성도 추적해 숨김 시 애니메이션을 즉시 멈춥니다.</summary>
    internal sealed class VisibilityWatch : IDisposable
    {
        readonly Control owner;readonly Action<bool> changed;
        readonly System.Collections.Generic.List<Control> parents=new System.Collections.Generic.List<Control>();
        public VisibilityWatch(Control control,Action<bool> action){owner=control;changed=action;owner.HandleCreated+=Rebind;owner.ParentChanged+=Rebind;owner.VisibleChanged+=Update;Rebind(null,EventArgs.Empty);}
        void Rebind(object sender,EventArgs e){foreach(var parent in parents){parent.VisibleChanged-=Update;parent.ParentChanged-=Rebind;}parents.Clear();for(Control parent=owner.Parent;parent!=null;parent=parent.Parent){parents.Add(parent);parent.VisibleChanged+=Update;parent.ParentChanged+=Rebind;}Update(null,EventArgs.Empty);}
        void Update(object sender,EventArgs e){if(!owner.IsDisposed)changed(owner.Visible);}
        public void Dispose(){owner.HandleCreated-=Rebind;owner.ParentChanged-=Rebind;owner.VisibleChanged-=Update;foreach(var parent in parents){parent.VisibleChanged-=Update;parent.ParentChanged-=Rebind;}parents.Clear();}
    }
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
    public class SoftInput : UserControl,IThemed
    {
        readonly TextBox editor;
        readonly SlimTextScroll scroll;
        ScrollBars scrollMode;
        public SoftInput() {
            SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);
            AutoScaleMode=AutoScaleMode.None;Size=new Size(260,40);TabStop=false;
            editor=new TextBox {BorderStyle=BorderStyle.None,Font=Theme.FontBase(),TabIndex=0};
            editor.TextChanged+=delegate{OnTextChanged(EventArgs.Empty);};
            editor.GotFocus+=delegate{Invalidate();};editor.LostFocus+=delegate{Invalidate();};
            scroll=new SlimTextScroll(editor){Visible=false};
            Controls.AddRange(new Control[]{editor,scroll});Font=Theme.FontBase();ApplyTheme();
        }
        public override string Text {get{return editor==null?base.Text:editor.Text;}set{if(editor==null)base.Text=value;else editor.Text=value??"";}}
        public bool Multiline {get{return editor.Multiline;}set{editor.Multiline=value;PerformLayout();}}
        public bool ReadOnly {get{return editor.ReadOnly;}set{editor.ReadOnly=value;}}
        public bool AcceptsReturn {get{return editor.AcceptsReturn;}set{editor.AcceptsReturn=value;}}
        public bool AcceptsTab {get{return editor.AcceptsTab;}set{editor.AcceptsTab=value;}}
        public bool UseSystemPasswordChar {get{return editor.UseSystemPasswordChar;}set{editor.UseSystemPasswordChar=value;}}
        public int MaxLength {get{return editor.MaxLength;}set{editor.MaxLength=value;}}
        public int SelectionStart {get{return editor.SelectionStart;}set{editor.SelectionStart=value;}}
        public int SelectionLength {get{return editor.SelectionLength;}set{editor.SelectionLength=value;}}
        public ScrollBars ScrollBars {get{return scrollMode;}set{scrollMode=value;editor.ScrollBars=System.Windows.Forms.ScrollBars.None;scroll.Visible=value!=System.Windows.Forms.ScrollBars.None;PerformLayout();}}
        public void SelectAll(){editor.SelectAll();}
        public void Select(int start,int length){editor.Select(start,length);}
        public void Clear(){editor.Clear();}
        public void AppendText(string value){editor.AppendText(value);}
        public new bool Focus(){return editor.Focus();}
        protected override void OnEnter(EventArgs e){base.OnEnter(e);if(editor!=null)editor.Focus();}
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(editor!=null){editor.Font=Font;PerformLayout();}}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(editor==null)return;int pad=Dpi.S(12);int h=editor.Multiline?Math.Max(18,Height-Dpi.S(22)):editor.PreferredHeight;editor.SetBounds(pad,Math.Max(Dpi.S(8),(Height-h)/2),Math.Max(10,Width-pad*2-(scrollMode==System.Windows.Forms.ScrollBars.None?0:Dpi.S(10))),h);if(scroll!=null)scroll.SetBounds(Width-Dpi.S(17),Dpi.S(12),Dpi.S(8),Math.Max(10,Height-Dpi.S(24)));}
        public void ApplyTheme(){BackColor=Theme.Field;ForeColor=Theme.TextPrimary;if(editor!=null){editor.BackColor=Theme.Field;editor.ForeColor=Theme.TextPrimary;}Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){PaintSurface(e.Graphics,this,ContainsFocus);}
        internal static void PaintSurface(Graphics g,Control control,bool focus){var p=Theme.Current;using(var background=new SolidBrush(control.Parent==null?p.Bg:control.Parent.BackColor))g.FillRectangle(background,control.ClientRectangle);g.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=Theme.RoundedRect(new Rectangle(0,0,control.Width-1,control.Height-1),Dpi.S(12)))using(var fill=new SolidBrush(p.Field))using(var edge=new Pen(focus?p.Accent:p.Border,focus?1.5f:1f)){g.FillPath(fill,shape);g.DrawPath(edge,shape);}}
    }
    public class SlimTextScroll : Control
    {
        readonly TextBox target;bool dragging;readonly Timer refresh=new Timer{Interval=150};readonly VisibilityWatch visibility;
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
        public SlimTextScroll(TextBox editor){target=editor;DoubleBuffered=true;TabStop=false;Cursor=Cursors.Hand;target.TextChanged+=delegate{Invalidate();};refresh.Tick+=delegate{if(Visible)Invalidate();};visibility=new VisibilityWatch(this,delegate(bool visible){if(visible)refresh.Start();else refresh.Stop();});}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible)refresh.Start();else refresh.Stop();}
        int Lines {get{return Math.Max(1,target.GetLineFromCharIndex(target.TextLength)+1);}}
        int VisibleLines {get{return Math.Max(1,target.Height/Math.Max(1,target.Font.Height));}}
        void MoveTo(int y){if(!target.IsHandleCreated)return;int first=(int)SendMessage(target.Handle,0x00CE,IntPtr.Zero,IntPtr.Zero);int next=(int)(Math.Max(0,Lines-VisibleLines)*Math.Max(0,Math.Min(1,y/(double)Math.Max(1,Height))));SendMessage(target.Handle,0x00B6,IntPtr.Zero,(IntPtr)(next-first));Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){dragging=true;Capture=true;MoveTo(e.Y);base.OnMouseDown(e);}
        protected override void OnMouseMove(MouseEventArgs e){if(dragging)MoveTo(e.Y);base.OnMouseMove(e);}
        protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Field);if(!target.IsHandleCreated||Lines<=VisibleLines)return;int h=Math.Min(Height,Math.Max(Dpi.S(22),Height*VisibleLines/Lines));int first=(int)SendMessage(target.Handle,0x00CE,IntPtr.Zero,IntPtr.Zero);int y=(Height-h)*first/Math.Max(1,Lines-VisibleLines);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.RoundedRect(new Rectangle(1,Math.Min(Height-h,Math.Max(0,y)),Math.Max(2,Width-2),h),Dpi.S(4)))using(var b=new SolidBrush(Theme.Current.FieldBorder))e.Graphics.FillPath(b,p);}
        protected override void Dispose(bool disposing){if(disposing){visibility.Dispose();refresh.Dispose();}base.Dispose(disposing);}
    }
    public class SoftScrollBar : Control,IThemed
    {
        int value,maximum,largeChange=1;bool dragging;
        public SoftScrollBar(){DoubleBuffered=true;Width=Dpi.S(12);TabStop=false;Cursor=Cursors.Hand;}
        public int Maximum{get{return maximum;}set{maximum=Math.Max(0,value);Invalidate();}}
        public int LargeChange{get{return largeChange;}set{largeChange=Math.Max(1,value);Invalidate();}}
        public int SmallChange{get;set;}
        public int Value{get{return value;}set{int next=Math.Max(0,Math.Min(Math.Max(0,maximum-largeChange),value));if(this.value==next)return;this.value=next;Invalidate();var h=Scroll;if(h!=null)h(this,EventArgs.Empty);}}
        public event EventHandler Scroll;
        int ThumbHeight{get{return Math.Min(Height,Math.Max(Dpi.S(28),Height*largeChange/Math.Max(1,maximum)));}}
        void Seek(int y){Value=(int)(Math.Max(0,maximum-largeChange)*Math.Max(0,Math.Min(1,(y-ThumbHeight/2.0)/Math.Max(1,Height-ThumbHeight))));}
        protected override void OnMouseDown(MouseEventArgs e){dragging=true;Capture=true;Seek(e.Y);base.OnMouseDown(e);}
        protected override void OnMouseMove(MouseEventArgs e){if(dragging)Seek(e.Y);base.OnMouseMove(e);}
        protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
        public void ApplyTheme(){Invalidate();}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Panel:Parent.BackColor);if(!Enabled||maximum<=largeChange)return;int h=ThumbHeight,y=(Height-h)*value/Math.Max(1,maximum-largeChange);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.RoundedRect(new Rectangle(3,y,Math.Max(2,Width-6),h),Dpi.S(4)))using(var b=new SolidBrush(Theme.Current.FieldBorder))e.Graphics.FillPath(b,path);}
    }
    public class SoftDatePicker : DateTimePicker,IThemed
    {
        public void ApplyTheme(){CalendarMonthBackground=Theme.Field;CalendarForeColor=Theme.TextPrimary;Font=Theme.FontBase();Invalidate();}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if((m.Msg==0x000F||m.Msg==0x0317||m.Msg==0x0318)&&IsHandleCreated&&Width>30)using(var g=m.Msg==0x000F?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)){SoftInput.PaintSurface(g,this,Focused);TextRenderer.DrawText(g,Value.ToString("yyyy.MM.dd"),Font,new Rectangle(10,0,Width-38,Height),Enabled?Theme.TextPrimary:Theme.TextMuted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);g.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(Theme.TextMuted,1.5f)){int x=Width-16,y=Height/2;g.DrawLine(pen,x-3,y-2,x,y+1);g.DrawLine(pen,x,y+1,x+3,y-2);}}}
    }
    public class SoftNumber : UserControl,IThemed
    {
        readonly TextBox editor;readonly SoftButton minus,plus;decimal number,minimum,maximum=100;bool syncing;
        public SoftNumber(){AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;Size=new Size(144,38);TabStop=false;
            editor=new TextBox{BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Center,Font=Theme.FontBase(),Text="0"};
            minus=new SoftButton{Text="−",TabStop=false};plus=new SoftButton{Text="+",TabStop=false};
            minus.Click+=delegate{Value=Math.Max(Minimum,Value-1);};plus.Click+=delegate{Value=Math.Min(Maximum,Value+1);};
            editor.TextChanged+=delegate{decimal v;if(!syncing&&decimal.TryParse(editor.Text,out v)&&v==decimal.Truncate(v)&&v>=Minimum&&v<=Maximum&&v!=number){number=v;OnValueChanged();}};
            editor.Validated+=delegate{decimal v;if(decimal.TryParse(editor.Text,out v))Value=Math.Max(Minimum,Math.Min(Maximum,decimal.Truncate(v)));Sync();};
            editor.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Up){plus.PerformClick();e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Down){minus.PerformClick();e.SuppressKeyPress=true;}};
            editor.GotFocus+=delegate{Invalidate();};editor.LostFocus+=delegate{Invalidate();};
            Controls.AddRange(new Control[]{editor,minus,plus});ApplyTheme();}
        public decimal Minimum {get{return minimum;}set{minimum=value;if(number<minimum)Value=minimum;}}
        public decimal Maximum {get{return maximum;}set{maximum=value;if(number>maximum)Value=maximum;}}
        public decimal Value {get{return number;}set{var next=Math.Max(minimum,Math.Min(maximum,value));bool changed=next!=number;number=next;Sync();if(changed)OnValueChanged();}}
        public event EventHandler ValueChanged;
        void OnValueChanged(){var h=ValueChanged;if(h!=null)h(this,EventArgs.Empty);}
        void Sync(){if(editor==null)return;syncing=true;editor.Text=number.ToString("0");syncing=false;}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(editor==null)return;int w=Dpi.S(30),gap=Dpi.S(5);minus.SetBounds(gap,gap,w,Height-gap*2);plus.SetBounds(Width-w-gap,gap,w,Height-gap*2);editor.SetBounds(w+gap*2,(Height-editor.PreferredHeight)/2,Math.Max(20,Width-w*2-gap*4),editor.PreferredHeight);}
        public void ApplyTheme(){BackColor=Theme.Field;ForeColor=Theme.TextPrimary;if(editor!=null){editor.BackColor=Theme.Field;editor.ForeColor=Theme.TextPrimary;editor.Font=Theme.FontBase();minus.ApplyTheme();plus.ApplyTheme();}Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){SoftInput.PaintSurface(e.Graphics,this,ContainsFocus);}
    }
    public static class AppDialog
    {
        public static DialogResult Show(string text){return Show(null,text,Brand.Name);}
        public static DialogResult Show(string text,string caption){return Show(null,text,caption);}
        public static DialogResult Show(IWin32Window owner,string text){return Show(owner,text,Brand.Name);}
        public static DialogResult Show(IWin32Window owner,string text,string caption){return Show(owner,text,caption,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons){return Show(owner,text,caption,buttons,MessageBoxIcon.Information);}
        public static DialogResult Show(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){return Show(null,text,caption,buttons,icon);}
        public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){using(var form=Create(text,caption,buttons,icon)){return owner==null?form.ShowDialog():form.ShowDialog(owner);}}
        public static Form Create(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){
            var f=new Form{Text=caption,Font=Theme.FontBase(),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.None};
            using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.app.ico"))if(stream!=null)f.Icon=new Icon(stream);
            int textHeight=TextRenderer.MeasureText(text,Theme.FontBase(),new Size(424,2000),TextFormatFlags.WordBreak).Height;
            int bodyHeight=Math.Min(Math.Max(140,textHeight+76),540);f.ClientSize=new Size(520,bodyHeight+100);
            var card=new SoftCard{Left=20,Top=20,Width=480,Height=bodyHeight,AutoScroll=true};
            var title=Design.Label(icon==MessageBoxIcon.Error?"확인이 필요해요":icon==MessageBoxIcon.Warning?"잠깐 확인해 주세요":"EXP 안내",28,22,424,28,15,"t");
            var body=Design.Label(text,28,66,424,textHeight+12,10,"m");card.Controls.AddRange(new Control[]{title,body});f.Controls.Add(card);
            string[] names;DialogResult[] values;
            if(buttons==MessageBoxButtons.YesNo){names=new[]{"확인","취소"};values=new[]{DialogResult.Yes,DialogResult.No};}
            else if(buttons==MessageBoxButtons.YesNoCancel){names=new[]{"예","아니오","취소"};values=new[]{DialogResult.Yes,DialogResult.No,DialogResult.Cancel};}
            else if(buttons==MessageBoxButtons.OKCancel){names=new[]{"확인","취소"};values=new[]{DialogResult.OK,DialogResult.Cancel};}
            else{names=new[]{"확인"};values=new[]{DialogResult.OK};}
            for(int i=0;i<names.Length;i++){var b=Design.Button(names[i],500-(names.Length-i)*116,bodyHeight+40,104,i==0);b.DialogResult=values[i];f.Controls.Add(b);if(i==0)f.AcceptButton=b;if(i==names.Length-1)f.CancelButton=b;}
            Design.Scale(f);Theme.Apply(f);return f;
        }
    }

    /// <summary>대화상자 공통 제목 띠. 오프화이트·네이비·코랄 체계를 따릅니다.</summary>
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
            var g=e.Graphics; var p=Theme.Current; int pad=Dpi.S(24);
            g.Clear(Parent==null?p.Bg:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new Rectangle(0,0,Width-1,Height-1);
            using(var shape=Theme.RoundedRect(r,Dpi.S(16)))
            using(var fill=new SolidBrush(p.Card))using(var edge=new Pen(p.Border)){g.FillPath(fill,shape);g.DrawPath(edge,shape);}
            using(var line=new SolidBrush(p.Accent))g.FillRectangle(line,pad,Dpi.S(26),Dpi.S(4),Height-Dpi.S(52));
            bool action=ActionText.Length>0 && Width>Dpi.S(740);
            int textWidth=Width-pad*2-Dpi.S(18)-(action?Dpi.S(200):0),x=pad+Dpi.S(18);
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,Title,Theme.Font(23f,FontStyle.Bold),new Rectangle(x,Dpi.S(25),textWidth,Dpi.S(44)),p.Text,flags);
            TextRenderer.DrawText(g,Subtitle,Theme.Font(10f,FontStyle.Bold),new Rectangle(x,Dpi.S(78),textWidth,Dpi.S(28)),p.TextMuted,flags);
            if(action) {
                var cta=new Rectangle(Width-pad-Dpi.S(180),Height/2-Dpi.S(22),Dpi.S(180),Dpi.S(44));
                using(var shape=Theme.RoundedRect(cta,Dpi.S(10)))using(var fill=new SolidBrush(hot?p.AccentHover:p.Accent))g.FillPath(fill,shape);
                TextRenderer.DrawText(g,ActionText,Theme.Font(10f,FontStyle.Bold),cta,p.OnAccent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }
            if(Focused&&ShowFocusCues)using(var pen=new Pen(p.Accent,2))using(var shape=Theme.RoundedRect(new Rectangle(4,4,Width-9,Height-9),Dpi.S(12)))g.DrawPath(pen,shape);
        }
    }
    /// <summary>고객 앱과 관리자에서 공유하는 표면입니다.</summary>
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
        readonly Timer motion=new Timer { Interval=16 };readonly VisibilityWatch visibility;
        public SoftButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Height = 40; Cursor = Cursors.Hand; Font = Theme.FontBase(); motion.Tick+=delegate {float target=hot?1:0;hover+=(target-hover)*.3f;if(Math.Abs(target-hover)<.02f){hover=target;motion.Stop();}Invalidate();}; visibility=new VisibilityWatch(this,delegate(bool visible){if(!visible){motion.Stop();hover=0;hot=down=false;}}); }
        protected override void Dispose(bool disposing) { if(disposing){visibility.Dispose();motion.Dispose();}base.Dispose(disposing); }
        public void ApplyTheme() { BackColor = Theme.Panel; ForeColor = Theme.TextPrimary; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hot = true; motion.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = down = false; motion.Start(); base.OnMouseLeave(e); }
        protected override void OnVisibleChanged(EventArgs e) { if(!Visible){motion.Stop();hover=0;hot=down=false;}base.OnVisibleChanged(e); }
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
            // 외부 그림 없이 그리는 EXP의 E 표식입니다.
            using (var pen = new Pen(Theme.Current.OnAccent, Math.Max(2,Width/13f))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen,Width*.29f,Height*.29f,Width*.70f,Height*.29f); g.DrawLine(pen,Width*.29f,Height*.50f,Width*.60f,Height*.50f); g.DrawLine(pen,Width*.29f,Height*.71f,Width*.70f,Height*.71f); }
        }
    }
    public class SoftCombo : ComboBox
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeRect { public int Left,Top,Right,Bottom; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct ComboInfo { public int Size; public NativeRect Item,Button; public int State; public IntPtr Combo,Edit,List; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr handle,ref ComboInfo info);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
        void AlignEditor() {
            if(!IsHandleCreated || DropDownStyle!=ComboBoxStyle.DropDown)return;
            var info=new ComboInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf(typeof(ComboInfo))};
            if(!GetComboBoxInfo(Handle,ref info)||info.Edit==IntPtr.Zero)return;
            int h=Math.Min(Height-Dpi.S(8),TextRenderer.MeasureText("가Ag",Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Height);
            SetWindowPos(info.Edit,IntPtr.Zero,Dpi.S(12),Math.Max(0,(Height-h)/2),Math.Max(1,Width-Dpi.S(44)),h,0x0014);
        }
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if(m.Msg==0x0005 || m.Msg==0x0030 || m.Msg==0x0047 || m.Msg==0x000F)AlignEditor();
            if((m.Msg==0x000F || m.Msg==0x0317 || m.Msg==0x0318) && IsHandleCreated && Width>30 && DropDownStyle!=ComboBoxStyle.Simple)using(var g=m.Msg==0x000F?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)) {
                var p=Theme.Current;int w=Dpi.S(22);var r=new Rectangle(Width-w-1,1,w,Height-2);
                SoftInput.PaintSurface(g,this,Focused);
                TextRenderer.DrawText(g,Text,Font,new Rectangle(Dpi.S(12),0,Width-Dpi.S(38),Height),Enabled?p.Text:p.TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);
                g.SmoothingMode=SmoothingMode.AntiAlias;float x=r.Left+r.Width/2f,y=Height/2f;
                using(var pen=new Pen(Enabled?p.TextMuted:p.Border,Dpi.S(2))) {g.DrawLine(pen,x-Dpi.S(3),y-Dpi.S(1),x,y+Dpi.S(2));g.DrawLine(pen,x,y+Dpi.S(2),x+Dpi.S(3),y-Dpi.S(1));}
            }
        }
        public SoftCombo() { DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=Dpi.S(30);DropDownStyle=ComboBoxStyle.DropDownList;Font=Theme.FontBase(); }
        protected override void ScaleControl(SizeF factor,BoundsSpecified specified){base.ScaleControl(factor,specified);ItemHeight=Dpi.S(30);AlignEditor();}
        protected override void OnDrawItem(DrawItemEventArgs e) {
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var b=new SolidBrush(selected?Theme.Current.Selected:Theme.Current.Field))e.Graphics.FillRectangle(b,e.Bounds);
            string text=e.Index>=0?Convert.ToString(Items[e.Index]):Text;
            TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+Dpi.S(12),e.Bounds.Y,e.Bounds.Width-Dpi.S(24),e.Bounds.Height),Enabled?Theme.TextPrimary:Theme.TextMuted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
        }
    }
    public static class Design
    {
        public static Label Label(string text, int x, int y, int w, int h, float size, string tone)
        { return new Label { Text = text, Left = x, Top = y, Width = w, Height = h, Font = Theme.Font(size, FontStyle.Bold), Tag = tone, ForeColor = tone == "m" ? Theme.TextMuted : Theme.TextPrimary }; }
        public static SoftButton Button(string text, int x, int y, int w, bool primary)
        { return new SoftButton { Text = text, Left = x, Top = y, Width = w, Primary = primary }; }
        public static SoftInput Input(string name, int x, int y, int w, int h, bool readOnly)
        { return new SoftInput { Name = name, Left = x, Top = y, Width = w, Height = h, Font = Theme.FontBase(), Multiline = true, ReadOnly = readOnly, ScrollBars = ScrollBars.None, MaxLength = Lumivolt.Security.LicenseProtocol.MaxText }; }
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
