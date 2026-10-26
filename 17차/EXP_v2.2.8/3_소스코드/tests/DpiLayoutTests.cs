using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BunjangAuto;
class DpiLayoutTests {
    static int passed;
    [StructLayout(LayoutKind.Sequential)]struct Rect {public int L,T,R,B;}
    [StructLayout(LayoutKind.Sequential)]struct Info {public int Size;public Rect Item,Button;public int State;public IntPtr Combo,Edit,List;}
    [DllImport("user32.dll")]static extern bool GetComboBoxInfo(IntPtr h,ref Info i);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect r);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static void Show(Form f){f.ShowInTaskbar=false;f.Opacity=0;f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();}
    static void Save(Form f,string path){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(path);}}
    static Control Find(Control parent,string name){foreach(Control c in parent.Controls){if(c.Name==name)return c;var found=Find(c,name);if(found!=null)return found;}return null;}
    static void Validate(LicensePage p,string mode){
        var cards=p.Controls.OfType<CardPanel>().OrderBy(c=>c.Top).ToArray();
        Check(cards.Length==3,"three cards "+mode);
        Check(cards[0].Bottom<cards[1].Top&&cards[1].Bottom<cards[2].Top,"cards never overlap "+mode);
        Check(cards.All(c=>c.Left==cards[0].Left&&c.Width==cards[0].Width),"consistent card edges "+mode);
        foreach(var card in cards){
            var controls=card.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
            Check(controls.All(c=>c.Left>=0&&c.Top>=0&&c.Right<=card.ClientSize.Width&&c.Bottom<=card.ClientSize.Height),"children fit "+card.Name+" "+mode);
            for(int a=0;a<controls.Length;a++)for(int b=a+1;b<controls.Length;b++)
                Check(!controls[a].Bounds.IntersectsWith(controls[b].Bounds),"no overlap "+card.Name+" "+a+"/"+b+" "+mode);
        }
        var key=Find(p,"licenseKeyInput");var ok=Find(p,"activateLicense");
        Check(ok.Top>=key.Bottom+Ui.S(10),"key/button gap "+mode);
        foreach(var input in new[]{(SoftInput)key,(SoftInput)Find(p,"creditInput")}){
            var editor=input.Controls.OfType<TextBox>().Single();
            Check(editor.Top>=Ui.S(8)&&editor.Bottom<=input.Height-Ui.S(8),"input inner padding "+mode);
        }
    }
    [STAThread]static int Main(string[] args){
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};
        try{Run(args);Console.WriteLine("DPI: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string[] args){
        Dpi.ForcedScale=double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture);Dpi.EmulateFontScale=true;
        AppFont.Init();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(args[0]);
        Check(AppFont.Active&&Theme.FontBase().Bold,"embedded Nanum Gothic Bold active");
        foreach(bool dark in new[]{false,true}){
            Theme.Set(dark?ThemePalette.DarkPal:ThemePalette.Light);string mode=dark?"Dark":"Light";
            using(var f=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(Ui.S(960),Ui.S(1040)),Font=Theme.FontBase()}){
                var page=new LicensePage(new AppHost());Ui.ScaleTree(page);f.Controls.Add(page);Theme.Apply(f);Show(f);
                foreach(var pair in new[]{new[]{"lblName","EXP Classic / 테스트 사용자"},new[]{"lblExp","2026-11-01 23:59"},new[]{"lblLeft","20일 남음"},new[]{"lblPc","F00333C5A86DD6EC"}})
                    ((Label)typeof(LicensePage).GetField(pair[0],BindingFlags.Instance|BindingFlags.NonPublic).GetValue(page)).Text=pair[1];
                page.LayoutForDpi();Application.DoEvents();Validate(page,mode+" wide");Save(f,Path.Combine(args[0],mode+"-license-wide.png"));
                f.ClientSize=new Size(Ui.S(540),Ui.S(1040));Application.DoEvents();Validate(page,mode+" narrow");
                ((Label)typeof(LicensePage).GetField("lblMsg",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(page)).Text="인증키 형식을 확인해 주세요. 관리자에게 받은 전체 키를 다시 붙여 넣으세요.";
                Application.DoEvents();Validate(page,mode+" message");Save(f,Path.Combine(args[0],mode+"-license-narrow.png"));
                page.AutoScrollPosition=new Point(0,Ui.S(100));f.ClientSize=new Size(Ui.S(720),Ui.S(600));Application.DoEvents();Validate(page,mode+" scrolled resize");
                f.ClientSize=new Size(Ui.S(960),Ui.S(1040));page.AutoScrollPosition=Point.Empty;Application.DoEvents();Validate(page,mode+" restored");
            }
            using(var f=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(490,195),Font=Theme.FontBase()}){
                int y=20;foreach(var label in new[]{"디지털","오디오/영상/관련기기","기타 오디오/영상/관련기기"}){
                    var combo=new SoftCombo{DropDownStyle=ComboBoxStyle.DropDown,Left=24,Top=y,Width=430};combo.Items.AddRange(new object[]{label,"다른 항목"});combo.SelectedIndex=0;f.Controls.Add(combo);y+=48;
                }
                Design.Scale(f);Theme.Apply(f);Show(f);
                foreach(var combo in f.Controls.OfType<SoftCombo>()){
                    combo.Refresh();Application.DoEvents();var info=new Info{Size=Marshal.SizeOf(typeof(Info))};GetComboBoxInfo(combo.Handle,ref info);Rect r;GetWindowRect(info.Edit,out r);
                    var origin=combo.PointToScreen(Point.Empty);int top=r.T-origin.Y,left=r.L-origin.X;
                    Check(left>=Ui.S(10),"combo horizontal padding "+mode);
                    Check(Math.Abs(top-(combo.Height-(r.B-r.T))/2)<=2,"combo vertical center "+mode);
                    combo.Text="한글 직접 입력";Check(combo.Text=="한글 직접 입력","editable Korean value "+mode);combo.SelectedIndex=0;
                    combo.Focus();combo.DroppedDown=true;Check(combo.DroppedDown,"dropdown opens "+mode);combo.SelectedIndex=1;combo.DroppedDown=false;Check(combo.Text=="다른 항목","dropdown selection "+mode);combo.SelectedIndex=0;
                }
                Save(f,Path.Combine(args[0],mode+"-categories.png"));
            }
            using(var f=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(Ui.S(700),Ui.S(900)),Font=Theme.FontBase()}){
                var host=new AppHost{Cfg=new AppSettings{RepeatAll=true,RepeatCount=int.MaxValue},Sel=new SelectorSet(),Products=new System.ComponentModel.BindingList<Product>(),Runner=new Runner()};
                host.Auto=new AutomationController(host.Cfg,host.Sel,host.Products,host.Runner,f);
                host.Auto.StateText="실행 중... (99999/99999) · 999999/2147483647회차";
                typeof(AutomationController).GetField("_roundTotal",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host.Auto,int.MaxValue);
                var page=new AutomationPage(host);Ui.ScaleTree(page);f.Controls.Add(page);Theme.Apply(f);Show(f);page.LayoutForDpi();
                var state=Find(page,"automationState");var badge=Find(page,"automationBadge");
                Check(!state.Bounds.IntersectsWith(badge.Bounds),"huge repeat count cannot overlap badge "+mode);
                var cards=page.Controls.OfType<CardPanel>().OrderBy(c=>c.Top).ToArray();
                Check(cards[0].Bottom<cards[1].Top,"automation cards stay separated "+mode);
                var widgets=cards[0].Controls.Cast<Control>().ToArray();
                for(int a=0;a<widgets.Length;a++)for(int b=a+1;b<widgets.Length;b++)Check(!widgets[a].Bounds.IntersectsWith(widgets[b].Bounds),"automation no overlap "+a+"/"+b+" "+mode);
                Save(f,Path.Combine(args[0],mode+"-automation.png"));
            }
            using(var f=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(1020,330),Font=Theme.FontBase()}){
                var product=new Product{ShippingFee=3500,HalfShipping=true,HalfShippingCarrier="GS_HALF_PRICE",HalfShippingFee=1800,RememberShipping=true};
                var editor=new ShippingEditor(product){Left=24,Top=24};f.Controls.Add(editor);Design.Scale(f);Theme.Apply(f);Show(f);editor.ValidateValues();
                var saved=new Product();editor.Save(saved);Check(saved.ShippingFee==3500&&saved.HalfShippingFee==1800&&saved.HalfShippingCarrier=="GS_HALF_PRICE"&&saved.RememberShipping,"shipping editor saves full settings "+mode);
                ((CheckBox)Find(editor,"shippingIncluded")).Checked=true;
                Check(!Find(editor,"shippingFee").Enabled&&!Find(editor,"halfShippingFee").Enabled,"included disables fee fields "+mode);
                ((CheckBox)Find(editor,"shippingIncluded")).Checked=false;
                ((RadioButton)Find(editor,"halfShippingNo")).Checked=true;
                Check(!Find(editor,"halfShippingFee").Enabled&&Find(editor,"shippingFee").Enabled,"half shipping independent toggle "+mode);
                ((RadioButton)Find(editor,"halfShippingYes")).Checked=true;
                Save(f,Path.Combine(args[0],mode+"-shipping.png"));
            }
            var item=new Product{Title="테스트 상품",Price="43000",Category="디지털>오디오/영상/관련기기>기타 오디오/영상/관련기기",Condition="새 상품(미사용)",ShippingFee=3500,HalfShipping=true,HalfShippingCarrier="ALL",HalfShippingFee=1800};
            using(var f=new EditForm(item)){
                Show(f);Save(f,Path.Combine(args[0],mode+"-product-top.png"));
                var ship=f.Controls.OfType<ShippingEditor>().Single();var save=Find(f,"saveProduct");
                Check(ship.Bottom<save.Top,"shipping card separate from save button "+mode);
                f.ScrollControlIntoView(save);Application.DoEvents();Save(f,Path.Combine(args[0],mode+"-product-shipping.png"));
                ((SoftInput)Find(ship,"shippingFee")).Text="4100";((Button)save).PerformClick();
                Check(item.ShippingFee==4100&&item.HalfShippingFee==1800&&item.HalfShipping,"product save includes shipping "+mode);
            }
            using(var f=new EditForm(item)){
                Show(f);((SoftInput)Find(f,"shippingFee")).Text="9900";
                ((Button)f.CancelButton).PerformClick();Check(item.ShippingFee==4100,"cancel preserves shipping "+mode);
            }
        }
    }
}
