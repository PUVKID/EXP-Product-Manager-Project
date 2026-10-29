using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using BunjangAuto;

/// <summary>TESTBUILD 전용 네이티브 렌더 검사입니다. 실행·인증·초기화 버튼은 누르지 않습니다.</summary>
class RedesignUiTests
{
    static int passed;
    static string output;
    static readonly string[] keys={"dashboard","products","automation","login","schedule","help","settings","license"};
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var item in All(child))yield return item;}
    static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
    static void Pump(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);while(DateTime.UtcNow<end){Application.DoEvents();System.Threading.Thread.Sleep(8);}}
    static void Save(Form form,string file){Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,file+".png"));}}
    static void StopHostTimers(MainForm form){Field<Timer>(form,"_guardTimer").Stop();Field<Timer>(form,"timer").Stop();}
    static void LockActions(Control root){if(root is Button||root is FluentButton||root is ToggleSwitch||root is TextBoxBase||root is ComboBox)root.Enabled=false;root.ControlAdded+=delegate(object sender,ControlEventArgs e){LockActions(e.Control);};foreach(Control child in root.Controls)LockActions(child);}
    static MainForm Make(string mode)
    {
        // 부모의 실제 상태를 읽거나 인증 갱신을 실행하지 않는 별도 TESTBUILD 자료입니다.
        LicenseGate.LastInfo=new LicenseInfo {Valid=false};
        Storage.SaveSettings(new AppSettings {ThemeMode=mode,AssumeLoggedIn=true});
        var form=new MainForm();StopHostTimers(form);
        var host=Field<AppHost>(form,"host");host.Products.Clear();
        host.Products.Add(new Product {Title="데일리 백팩 · 블랙",Price="39000",Quantity="10",Category="패션잡화>가방>백팩",Condition="새 상품(미사용)",Description="상품 설명",ShippingFee=3500,HalfShipping=true,HalfShippingFee=1800,HalfShippingCarrier="ALL"});
        host.Products.Add(new Product {Title="완료한 상품",Price="23000",Status="등록됨",LastPosted=DateTime.Now.ToString("s")});
        host.Products.Add(new Product {Title="상태를 확인할 상품",Price="12000",Status="실패: 확인 필요"});
        ((DashboardPage)All(form).First(c=>c is DashboardPage)).Refresh2();
        return form;
    }
    static void ValidateDashboard(MainForm form,string mode)
    {
        var page=(DashboardPage)All(form).First(c=>c is DashboardPage);page.LayoutForDpi();
        Check(!All(page).OfType<StatCard>().Any(),"분리된 네 카드 대신 통합 진행 패널 "+mode);
        Check(All(page).Count(c=>c.Name=="dashboardOverview")==1,"큰 등록 현황 영역 한 개 "+mode);
        Check(All(page).OfType<Label>().Single(c=>c.Name=="dashboardProgressTitle").Text=="현재 등록 진행","현재 진행으로 명확히 구분 "+mode);
        var values=new[]{"dashboardPending","dashboardDone","dashboardFail"}.Select(name=>All(page).Single(c=>c.Name==name)).ToArray();
        Check(values.Select(c=>c.Text).SequenceEqual(new[]{"1","1","1"}),"실제 대기 성공 실패 집계 "+mode);
        Check(values.Select(c=>c.Top).Distinct().Count()==1,"하단 통계 한 줄 "+mode);
        var palette=Theme.Current;
        foreach(var color in new[]{palette.Bg,palette.Sidebar,palette.Card,palette.CardAlt,palette.Field,palette.FieldBorder,palette.Border,palette.Hover,palette.Selected,palette.Accent,palette.AccentHover,palette.AccentPressed,palette.OnAccent,palette.Text,palette.TextMuted})
            Check(color.R==color.G&&color.G==color.B,"색조 없는 중립 팔레트 "+mode);
        Check(palette.Dark?palette.Accent.R>palette.OnAccent.R:palette.Accent.R<palette.OnAccent.R,"흑백 기본 버튼 대비 방향 "+mode);
        var panels=page.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
        for(int i=0;i<panels.Length;i++)for(int j=i+1;j<panels.Length;j++)Check(!panels[i].Bounds.IntersectsWith(panels[j].Bounds),"대시보드 영역 분리 "+i+"/"+j+" "+mode);
        var job=All(page).Single(c=>c.Name=="dashboardOverview");var children=job.Controls.Cast<Control>().ToArray();
        Check(children.All(c=>c.Left>=0&&c.Top>=0&&c.Right<=job.Width&&c.Bottom<=job.Height),"작업 패널 내부 배치 "+mode);
        for(int i=0;i<children.Length;i++)for(int j=i+1;j<children.Length;j++)Check(!children[i].Bounds.IntersectsWith(children[j].Bounds),"작업 패널 겹침 없음 "+i+"/"+j+" "+mode);
        var host=Field<AppHost>(form,"host");host.Auto.StateText="실행 중 (99999/99999) · 2147483647/2147483647회차";host.Auto.NextText="다음 실행까지 999시간 59분 59초 (12-31 23:59)";host.Auto.Total=99999;host.Auto.Done=33333;
        page.Refresh2();page.LayoutForDpi();
        var ring=All(page).OfType<BrandScene>().Single();var fraction=All(page).OfType<RunFraction>().Single();
        Check(ring.Maximum==99999&&ring.Value==33333&&ring.Percentage==33,"실제 현재 실행의 입체 브랜드 진행률 "+mode);
        Check(fraction.DisplayText=="33333 / 99999","큰 진행 숫자는 실제 Done Total "+mode);
        Check(All(page).Single(c=>c.Name=="openAutomation").Width==job.Width-Ui.S(48),"가로 전체 폭 자동 등록 버튼 "+mode);
        Check(All(page).OfType<Label>().Where(c=>c.Name=="dashboardState").All(c=>c.AutoEllipsis),"긴 상태의 안전한 말줄임 "+mode);
        host.Auto.Total=int.MaxValue;host.Auto.Done=int.MaxValue-1;page.Refresh2();
        Check(fraction.Done==int.MaxValue-1&&fraction.Total==int.MaxValue,"최대 정수 진행값 보존 "+mode);
        Check(fraction.Bounds.Right<ring.Left,"긴 숫자와 원형 영역 분리 "+mode);
    }
    static string RegistrySnapshot(string path)
    {
        using(var key=Registry.CurrentUser.OpenSubKey(path)){
            if(key==null)return "없음";var parts=new List<string>();
            foreach(var name in key.GetValueNames().OrderBy(v=>v)){var value=key.GetValue(name);parts.Add(name+":"+(value is byte[]?Convert.ToBase64String((byte[])value):Convert.ToString(value)));}
            foreach(var name in key.GetSubKeyNames().OrderBy(v=>v))parts.Add(name+"{"+RegistrySnapshot(path+"\\"+name)+"}");return string.Join("|",parts.ToArray());
        }
    }
    static string ProductionState(){return RegistrySnapshot(@"Software\EXP\Usage")+RegistrySnapshot(@"Software\Lumivolt");}
    // Shown 이벤트를 발생시키지 않아 진단 작업과 인증 메서드를 실행하지 않습니다.
    class DiagnosticPreview:DiagnosticsForm
    {
        public DiagnosticPreview(AppSettings config,IEnumerable<Product> products):base(config,products){}
        protected override void OnShown(EventArgs e){}
    }
    class MotionButton:FluentButton {public void Hover(){OnMouseEnter(EventArgs.Empty);}}
    class MotionNav:NavItem {public void SelectNow(){Selected=true;}}
    static void MotionTests()
    {
        using(var form=new Form {ClientSize=new Size(520,180),ShowInTaskbar=false,Opacity=0}){
            var button=new MotionButton{Left=12,Top=12,Text="상태 전환",Style=BtnStyle.Primary};var nav=new MotionNav{Left=12,Top=60,Width=220,Text="선택 전환"};var bar=new FluentProgress{Left=12,Top=120,Width=220};var ring=new CircularProgress{Left=450,Top=115,Width=50,Height=50};var soft=new SoftButton{Left=250,Top=12,Width=200,Text="공통 버튼"};
            var input=new SoftInput{Left=250,Top=60,Width=220,Height=50,Multiline=true,ScrollBars=ScrollBars.Vertical};
            form.Controls.AddRange(new Control[]{button,nav,bar,ring,soft,input});form.Show();Pump(30);
            typeof(SoftButton).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(soft,new object[]{EventArgs.Empty});
            var slim=input.Controls.OfType<SlimTextScroll>().Single();
            Check(Field<Timer>(soft,"motion").Enabled&&Field<Timer>(slim,"refresh").Enabled,"공통 버튼과 스크롤 타이머 시작");
            button.Hover();nav.SelectNow();bar.Value=90;ring.Maximum=100;ring.Value=75;
            Check(button.AnimationRunning&&nav.AnimationRunning&&bar.AnimationRunning&&ring.AnimationRunning,"호버 선택 진행 모션 시작");
            Pump(48);Save(form,"motion-running");
            form.Hide();Check(!button.AnimationRunning&&!nav.AnimationRunning&&!bar.AnimationRunning&&!ring.AnimationRunning,"숨김 시 모션 중지");
            Check(!Field<Timer>(soft,"motion").Enabled&&!Field<Timer>(slim,"refresh").Enabled,"숨김 시 공통 버튼과 스크롤 중지");
            form.Show();button.Hover();bar.Value=30;form.Dispose();Pump(30);Check(button.IsDisposed&&nav.IsDisposed&&bar.IsDisposed&&ring.IsDisposed,"닫힌 컨트롤 및 타이머 정리");
        }
    }
    [STAThread]static int Main(string[] args)
    {
        Console.OutputEncoding=new System.Text.UTF8Encoding(false);
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(stream==null)return null;using(var memory=new MemoryStream()){stream.CopyTo(memory);return Assembly.Load(memory.ToArray());}}};
        try{return Run(args);}catch(Exception ex){Console.WriteLine(ex);Console.WriteLine("REDESIGN: "+passed+" 통과, 1 실패");return 1;}
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
#if !TESTBUILD
        throw new InvalidOperationException("TESTBUILD 격리가 필요합니다.");
#else
        output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);Paths.EnsureAll();AppFont.Init();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Check(Paths.Root.Contains("Lumivolt-tests-"),"운영 자료와 다른 테스트 경로");Check(AppFont.Active&&Theme.FontBase().Bold,"나눔고딕 Bold 활성");
        string before=ProductionState();
        if(args.Contains("--preview")){
            var form=Make(args.Contains("--dark")?"Dark":"Light");LockActions(form);form.Text="EXP 20차 · 흑백 격리 UI 미리보기 (실행·인증 비활성)";form.ShowInTaskbar=true;
            var timeout=new Timer{Interval=60000};timeout.Tick+=delegate{timeout.Stop();timeout.Dispose();form.Dispose();Application.ExitThread();};form.Shown+=delegate{timeout.Start();};form.FormClosed+=delegate{timeout.Dispose();};Application.Run(form);Check(before==ProductionState(),"미리보기 운영 인증 기록 보존");return 0;
        }
        foreach(string mode in new[]{"Light","Dark"})using(var form=Make(mode)){
            form.ShowInTaskbar=false;form.Opacity=0;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Pump(25);
            Check(All(form).OfType<NavItem>().Count()==8,"메뉴 여덟 개 "+mode);
            Check(All(form).OfType<Segmented>().First().Items.SequenceEqual(new[]{"라이트","다크","시스템"}),"세 테마 유지 "+mode);
            foreach(string key in keys){form.Navigate(key);Pump(300);Theme.Apply(form);Save(form,mode+"-"+key);Check(All(form).OfType<PageBase>().Any(p=>p.Key==key&&p.Visible),"화면 열림 "+key+" "+mode);}
            form.Navigate("dashboard");ValidateDashboard(form,mode+" 넓음");Pump(300);Save(form,mode+"-dashboard-long-values");
            form.Size=new Size(Ui.S(980),Ui.S(700));Pump(30);ValidateDashboard(form,mode+" 좁음");Pump(300);Save(form,mode+"-dashboard-narrow");
            form.Navigate("products");Pump(25);Save(form,mode+"-products-narrow");
            var host=Field<AppHost>(form,"host");using(var edit=new EditForm(host.Products[0])){edit.ShowInTaskbar=false;edit.Opacity=0;edit.Show();Pump(20);Save(edit,mode+"-editor");Check(All(edit).Any(c=>c.Name=="productPhotos")&&All(edit).Any(c=>c.Name=="shippingFee"),"사진과 배송 편집 유지 "+mode);edit.Dispose();}
            using(var narrow=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(Ui.S(540),Ui.S(1000)),Font=Theme.FontBase(),ShowInTaskbar=false,Opacity=0}){
                var page=new DashboardPage(host);narrow.Controls.Add(page);page.Refresh2();Ui.ScaleTree(page);Theme.Apply(narrow);narrow.StartPosition=FormStartPosition.Manual;narrow.Location=new Point(-20000,-20000);narrow.Show();Pump(300);page.LayoutForDpi();
                var overview=All(page).Single(c=>c.Name=="dashboardOverview");var widgets=overview.Controls.Cast<Control>().ToArray();
                Check(widgets.All(c=>c.Left>=0&&c.Top>=0&&c.Right<=overview.Width&&c.Bottom<=overview.Height),"540 폭 현황 내부 배치 "+mode);
                for(int i=0;i<widgets.Length;i++)for(int j=i+1;j<widgets.Length;j++)Check(!widgets[i].Bounds.IntersectsWith(widgets[j].Bounds),"540 폭 현황 겹침 없음 "+i+"/"+j+" "+mode);
                Check(All(page).OfType<RunFraction>().Single().DisplayText=="2147483646 / 2147483647","540 폭에서도 큰 정수 그대로 유지 "+mode);
                Save(narrow,mode+"-dashboard-540");
            }
            using(var diagnostic=new DiagnosticPreview(host.Cfg,host.Products)){
                // Shown을 발생시키지 않고 공통 시각 상태만 렌더합니다. 실제 진단과 인증은 실행하지 않습니다.
                diagnostic.ShowInTaskbar=false;diagnostic.Opacity=0;diagnostic.StartPosition=FormStartPosition.Manual;diagnostic.Location=new Point(-20000,-20000);diagnostic.Show();Application.DoEvents();Save(diagnostic,mode+"-diagnostics-visual");Check(All(diagnostic).OfType<DiagnosticRow>().Count()==6,"자가진단 여섯 시각 행 "+mode);
            }
            using(var common=new Form{ClientSize=new Size(800,260),Font=Theme.FontBase()}){common.Controls.Add(new HeroBanner{Left=20,Top=20,Width=760,Height=136,Title="라이선스 발급 · 설정 초기화",Subtitle="관리자 및 초기화 도구의 공통 시각 구성"});common.Controls.Add(new SoftInput{Left=20,Top=180,Width=500});common.Controls.Add(new SoftButton{Left=540,Top=180,Width=200,Text="확인",Primary=true});Theme.Apply(common);common.ShowInTaskbar=false;common.Opacity=0;common.StartPosition=FormStartPosition.Manual;common.Location=new Point(-20000,-20000);common.Show();Application.DoEvents();Save(common,mode+"-admin-reset-shared-visual");}
        }
        MotionTests();Check(before==ProductionState(),"운영 HKCU 인증과 관리자 설정 불변");
        Console.WriteLine("REDESIGN: "+passed+" 통과, 0 실패");return 0;
#endif
    }
}
