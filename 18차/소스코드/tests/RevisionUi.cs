using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BunjangAuto;
class RevisionUi {
    static int fails,passes;
    static void Check(string name,bool good){Console.WriteLine((good?"PASS ":"FAIL ")+name);if(good)passes++;else fails++;}
    static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in All(child))yield return nested;}
    static void Show(Form f){f.ShowInTaskbar=false;f.Opacity=0;f.Show();Application.DoEvents();}
    static void Save(Form f,string file){Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height));b.Save(file);}}
    [STAThread]static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};return Run(args);}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args){try{
        if(args.Length>1){Dpi.ForcedScale=double.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture);Dpi.EmulateFontScale=true;}
        string target=Path.GetFullPath(args[0]);Directory.CreateDirectory(target);Paths.EnsureAll();AppFont.Init();Application.EnableVisualStyles();
        string photo=Path.Combine(Paths.Root,"example.png");using(var image=new Bitmap(120,90)){using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(40,80,210));g.FillEllipse(Brushes.LightSkyBlue,20,12,70,70);}image.Save(photo);}
        string corrupt=Path.Combine(Paths.Root,"corrupt.png");File.WriteAllText(corrupt,"not an image");
        Check("missing photo fails",DiagnosticChecks.InspectPhotos(new[]{photo+".missing"},1).State==DiagnosticState.Fail);
        Check("corrupt photo fails",DiagnosticChecks.InspectPhotos(new[]{corrupt},1).State==DiagnosticState.Fail);
        Check("valid photo passes",DiagnosticChecks.InspectPhotos(new[]{photo},1).State==DiagnosticState.Pass);
        Check("empty products are skipped",DiagnosticChecks.InspectPhotos(new string[0],0).State==DiagnosticState.Skipped);
        foreach(string mode in new[]{"Light","Dark"}){
            var cfg=new AppSettings{ThemeMode=mode,AssumeLoggedIn=true};Storage.SaveSettings(cfg);ThemeService.Init(cfg);
            var product=new Product {Title="디지털 상품 · 사진과 상세 설명을 확인해 주세요",Price="43000",Quantity="1",Category="디지털>오디오/영상/관련기기>기타 오디오/영상/관련기기",Condition="새 상품(미사용)",Tags="디지털,오디오,음악",Description="상품 구성과 사용 방법을 안내합니다.\r\n\r\n1. 구성품과 상태를 확인해 주세요.\r\n2. 궁금한 내용은 문의해 주세요.\r\n3. 구매 전 상품 설명을 읽어 주세요.",FreeShipping=true,Images=photo};
            using(var f=new EditForm(product)){Show(f);Save(f,Path.Combine(target,mode+"-product-editor.png"));var text=(TextBox)All(f).First(c=>c.Name=="productTitle");text.Text="저장 전 편집";Check("edit does not mutate until save "+mode,product.Title!="저장 전 편집");((Button)All(f).First(c=>c.Text=="취소")).PerformClick();Check("cancel preserves original "+mode,product.Title!="저장 전 편집");}
            using(var f=new EditForm(product)){Show(f);((TextBox)All(f).First(c=>c.Name=="productTitle")).Text="수정한 상품";((Button)All(f).First(c=>c.Name=="saveProduct")).PerformClick();Check("save preserves details and images "+mode,product.Title=="수정한 상품"&&product.Price=="43000"&&product.Images==photo&&product.FreeShipping);}
            LicenseGate.LastInfo=new LicenseInfo {Valid=true,Edition="Pro",Expires=DateTime.MaxValue,Name="테스트"};
            using(var f=new MainForm()){Show(f);var host=(AppHost)typeof(MainForm).GetField("host",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);host.Products.Clear();host.Products.Add(product);for(int i=0;i<5;i++)host.Products.Add(new Product {Title="등록한 상품 "+i,Price="1000",Status="등록됨"});f.Navigate("dashboard");((DashboardPage)All(f).First(c=>c is DashboardPage)).Refresh2();Save(f,Path.Combine(target,mode+"-dashboard.png"));Check("no log or admin navigation "+mode,!All(f).OfType<NavItem>().Any(c=>c.Key=="logs"||c.Key=="admin"));Check("one full-width recent activity "+mode,All(f).OfType<Label>().Count(c=>c.Text=="최근 활동")==1&&!All(f).OfType<Label>().Any(c=>c.Text=="최근 로그"));f.Navigate("settings");Save(f,Path.Combine(target,mode+"-settings.png"));Check("settings contains no telemetry/admin/log controls "+mode,!All(f).Any(c=>c.Text=="운영 로그 전송"||c.Text=="로그 폴더 열기"||c.Text=="관리자 로그인"));f.Navigate("help");Save(f,Path.Combine(target,mode+"-help.png"));}
            using(var f=new DiagnosticsForm(cfg,new[]{product})){Show(f);var until=DateTime.UtcNow.AddSeconds(20);while(!f.Completed&&DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(20);}Check("diagnostics complete "+mode,f.Completed);Save(f,Path.Combine(target,mode+"-diagnostics.png"));var rows=All(f).OfType<DiagnosticRow>().ToArray();Check("real pass and missing-license failure visible "+mode,rows.Any(r=>r.Result.State==DiagnosticState.Pass)&&rows.Any(r=>r.Title=="라이선스"&&r.Result.State==DiagnosticState.Fail));}
        }
        var asm=typeof(MainForm).Assembly;Check("no telemetry transmitter types",asm.GetType("BunjangAuto.TelemetryService")==null&&asm.GetType("BunjangAuto.CentralLogService")==null&&asm.GetType("BunjangAuto.AdminApiClient")==null);
        Check("help has no removed feature instructions",!HelpService.Topics().Any(t=>t.Markdown.Contains("운영 로그")||t.Markdown.Contains("로그 폴더")||t.Markdown.Contains("인터넷 없이")));
        }catch(Exception e){Console.WriteLine(e);fails++;}
        Console.WriteLine("REVISION: "+passes+" passed, "+fails+" failed");return fails==0?0:1;
    }
}
