using System;using System.IO;using System.Drawing;using System.Reflection;using System.Linq;using System.Windows.Forms;using BunjangAuto;
class SurfaceUiTests {
 static int count;static void Check(bool good,string name){if(!good)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static void Show(Form f){f.ShowInTaskbar=false;f.Opacity=0;f.Show();Application.DoEvents();}
 static void Save(Form f,string path){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(path);}}
 [STAThread]static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};return Run(args);}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]static int Run(string[] args){try{Directory.CreateDirectory(args[0]);AppFont.Init();Application.EnableVisualStyles();if(args.Length>1){Dpi.ForcedScale=1.25;Dpi.EmulateFontScale=true;}
 foreach(bool dark in new[]{false,true}){Theme.Set(dark?ThemePalette.DarkPal:ThemePalette.Light);string mode=dark?"Dark":"Light";
  using(var f=AppDialog.Create("이미 실행 중입니다.\r\n열려 있는 EXP 창에서 계속해 주세요.","EXP",MessageBoxButtons.OK,MessageBoxIcon.Information)){Show(f);Save(f,Path.Combine(args[0],mode+"-notice.png"));Check(f.AcceptButton!=null&&f.CancelButton!=null,"dialog keyboard actions "+mode);((Button)f.AcceptButton).PerformClick();Check(f.DialogResult==DialogResult.OK,"dialog confirmation "+mode);}
  using(var f=new Form{ClientSize=new Size(540,230)}){var input=new SoftInput{Left=24,Top=24,Width=490,Height=90,Multiline=true,ScrollBars=ScrollBars.Vertical};var number=new SoftNumber{Left=24,Top=140,Width=160,Minimum=1,Maximum=50,Value=20};f.Controls.AddRange(new Control[]{input,number});Theme.Apply(f);Show(f);
   int events=0;input.TextChanged+=delegate{events++;};input.Controls.OfType<TextBox>().Single().Text="한글 입력과 붙여넣기\r\n두 번째 줄";Check(input.Text.Contains("두 번째 줄")&&events==1,"input forwards edits "+mode);input.ReadOnly=true;Check(input.Controls.OfType<TextBox>().Single().ReadOnly,"readonly preserved "+mode);
   number.Controls.OfType<TextBox>().Single().Text="45";Check(number.Value==45,"typed numeric value "+mode);number.Controls.OfType<SoftButton>().Single(b=>b.Text=="+").PerformClick();Check(number.Value==46,"plus button value "+mode);number.Value=50;number.Controls.OfType<SoftButton>().Single(b=>b.Text=="+").PerformClick();Check(number.Value==50,"maximum respected "+mode);number.Value=1;number.Controls.OfType<SoftButton>().Single(b=>b.Text=="−").PerformClick();Check(number.Value==1,"minimum respected "+mode);Save(f,Path.Combine(args[0],mode+"-controls.png"));
  }
 }
 Console.WriteLine("SURFACES: "+count+" passed");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
