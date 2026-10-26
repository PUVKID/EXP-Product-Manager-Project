using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BunjangAuto;

/// <summary>운영 인증·게시 없이 새 예약 UI와 입체 모션을 검사합니다.</summary>
class RepeatDesignUiTests
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control c in root.Controls)foreach(var item in All(c))yield return item;}
    static T Find<T>(Control root,string name)where T:Control{return (T)All(root).Single(c=>c.Name==name);}
    static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();System.Threading.Thread.Sleep(5);}}
    static void Key(Segmented c,Keys key){typeof(Segmented).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{new KeyEventArgs(key)});}
    static void Save(Form f,string path){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(path);}}
    static void LayoutCheck(SchedulePage page,string caption){page.LayoutForDpi();var card=Find<CardPanel>(page,"scheduleCard");var list=card.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();Check(list.All(c=>c.Left>=0&&c.Top>=0&&c.Right<=card.Width&&c.Bottom<=card.Height),"예약 내부 경계 "+caption);for(int a=0;a<list.Length;a++)for(int b=a+1;b<list.Length;b++)Check(!list[a].Bounds.IntersectsWith(list[b].Bounds),"예약 겹침 없음 "+caption+" "+a+"/"+b);}
    [STAThread]static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};try{Run(args);Console.WriteLine("REPEAT DESIGN: "+passed+" 통과, 0 실패");return 0;}catch(Exception ex){Console.WriteLine(ex);Console.WriteLine("REPEAT DESIGN: "+passed+" 통과, 1 실패");return 1;}}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string[] args){
#if !TESTBUILD
        throw new InvalidOperationException("TESTBUILD 격리 필수");
#else
        Paths.EnsureAll();AppFont.Init();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);Check(Paths.Root.Contains("Lumivolt-tests-"),"운영 자료와 격리된 저장 경로");
        foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(bool dark in new[]{false,true}){
            Dpi.ForcedScale=scale;Dpi.EmulateFontScale=true;Theme.Set(dark?ThemePalette.DarkPal:ThemePalette.Light);string tag=(dark?"Dark":"Light")+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using(var form=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size(Ui.S(700),Ui.S(960)),Font=Theme.FontBase(),ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)}){
                var host=new AppHost{Cfg=new AppSettings(),Sel=new SelectorSet(),Products=new BindingList<Product>(),Runner=new Runner()};host.Auto=new AutomationController(host.Cfg,host.Sel,host.Products,host.Runner,form);LicenseGate.LastInfo=new LicenseInfo{Valid=true,Edition="Studio"};
                var page=new SchedulePage(host);form.Controls.Add(page);Theme.Apply(form);form.Show();page.OnActivated();Pump(10);
                var mode=Find<Segmented>(page,"registrationMode");var timing=Find<Segmented>(page,"batchTiming");var minutes=Find<SoftNumber>(page,"batchMinutes");var minute=Find<SoftNumber>(page,"batchMinute");
                mode.SelectedIndex=0;Check(!timing.Visible&&!Find<Control>(page,"timeRules").Visible,"한 번 모드의 불필요 입력 숨김 "+tag);LayoutCheck(page,tag+" once");
                mode.SelectedIndex=2;Check(host.Cfg.BatchRepeatEnabled&&!host.Cfg.OneByOne&&minutes.Visible&&!minute.Visible,"전체 목록 간격 선택 저장 "+tag);minutes.Value=1440;Check(host.Cfg.BatchRepeatMinutes==1440&&Storage.LoadSettings().BatchRepeatMinutes==1440,"최대 간격 저장 파일 복원 "+tag);Check(Find<Label>(page,"batchSummary").Text.StartsWith("1440분 간격으로 전체 목록 등록"),"변경한 간격 요약 표시 "+tag);
                timing.SelectedIndex=1;minute.Value=59;Check(host.Cfg.BatchRepeatClockAligned&&host.Cfg.BatchRepeatMinuteOfHour==59&&!minutes.Visible&&minute.Visible,"매시 지정분 저장 "+tag);Check(Find<Label>(page,"batchSummary").Text.StartsWith("매시 59분에 전체 목록 등록"),"59분 요약 표시 "+tag);minute.Value=20;Check(Find<Label>(page,"batchSummary").Text.StartsWith("매시 20분에 전체 목록 등록"),"20분 요약 즉시 갱신 "+tag);Check(minutes.Minimum==1&&minutes.Maximum==1440&&minute.Minimum==0&&minute.Maximum==59,"입력 허용 범위 "+tag);
                minute.Value=0;Check(Find<Label>(page,"batchSummary").Text.StartsWith("매시 00분에 전체 목록 등록"),"정각 00분 요약 표시 "+tag);minutes.Value=60;
                var editor=minute.Controls.OfType<TextBox>().Single();editor.Text="999999999999999999999999999999999999999";typeof(Control).GetMethod("OnValidated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,new object[]{EventArgs.Empty});Check(minute.Value==0&&editor.Text=="0","긴 숫자 입력 복구와 값 보존 "+tag);
                page.OnActivated();Check(timing.SelectedIndex==1&&minute.Value==0&&mode.SelectedIndex==2,"예약 재진입 설정 복원 "+tag);LayoutCheck(page,tag+" batch");Save(form,Path.Combine(output,tag+"-batch-clock.png"));
                host.Auto.Active=true;page.OnActivated();Check(!mode.Enabled&&!timing.Enabled&&!minute.Enabled,"실행 중 예약 잠금 "+tag);mode.SelectedIndex=0;Check(mode.SelectedIndex==2&&host.Cfg.BatchRepeatEnabled,"실행 중 프로그램 선택 변경도 거부 "+tag);host.Auto.Active=false;
                LicenseGate.LastInfo=new LicenseInfo{Valid=true,Edition="Classic"};host.Cfg.BatchRepeatEnabled=false;page.OnActivated();mode.SelectedIndex=2;Check(mode.SelectedIndex==0&&!host.Cfg.BatchRepeatEnabled,"Classic 전체 반복 선택 차단 "+tag);
                LicenseGate.LastInfo=new LicenseInfo{Valid=true,Edition="Pro"};page.OnActivated();mode.Focus();Key(mode,Keys.Right);Check(mode.SelectedIndex==1&&host.Cfg.OneByOne,"키보드 화살표 등록 방식 선택 "+tag);Find<ToggleSwitch>(page,"timeRulesEnabled").Checked=true;Find<ToggleSwitch>(page,"legacyRepeat").Checked=true;Find<SoftNumber>(page,"legacyRounds").Value=999;Check(host.Cfg.UseTimeRules&&host.Cfg.RepeatAll&&host.Cfg.RepeatCount==999,"레거시 시간대 횟수 반복 보존 "+tag);LayoutCheck(page,tag+" legacy");Save(form,Path.Combine(output,tag+"-legacy.png"));
                mode.SelectedIndex=2;timing.SelectedIndex=0;Check(Find<Label>(page,"batchSummary").Text.StartsWith("60분 간격으로 전체 목록 등록"),"60분 간격 요약 재선택 갱신 "+tag);LayoutCheck(page,tag+" interval");Save(form,Path.Combine(output,tag+"-batch-interval.png"));
                form.ClientSize=new Size(Ui.S(540),Ui.S(960));Pump(10);LayoutCheck(page,tag+" narrow");
                host.Cfg.BatchRepeatMinutes=60;host.Cfg.BatchRepeatMinuteOfHour=20;host.Cfg.BatchRepeatClockAligned=true;
                using(var automation=new AutomationPage(host)){form.Controls.Remove(page);form.Controls.Add(automation);automation.OnActivated();Pump(10);Check(All(automation).OfType<Label>().Any(l=>l.Text.Contains("전체 목록 · 매시 20분")),"자동 등록 화면 반복 방식 안내 "+tag);automation.OnDeactivated();form.Controls.Remove(automation);}
                page.Dispose();
            }
        }
        Dpi.ForcedScale=1;Dpi.EmulateFontScale=false;MotionPreferences.UserReducedMotion=false;
        using(var f=new Form{ClientSize=new Size(220,250),ShowInTaskbar=false,Opacity=0}){var scene=new BrandScene{Width=192,Height=220,Maximum=99999,Value=33333};f.Controls.Add(scene);f.Show();Pump(170);Check(BrandAssets.Sheet.Width==1536&&BrandAssets.Sheet.Height==1152&&BrandAssets.Still.Width==384,"48개 실제 3D 프레임과 정지 자산 크기");Check(scene.Percentage==33,"긴 진행값 백분율 보존");Check(MotionPreferences.ReducedMotion||scene.AnimationRunning,"표시 중 3D 모션");f.Hide();Check(!scene.AnimationRunning,"숨김 모션 정지");f.Show();f.WindowState=FormWindowState.Minimized;Pump(10);Check(!scene.AnimationRunning,"최소화 모션 정지");f.WindowState=FormWindowState.Normal;MotionPreferences.UserReducedMotion=true;Pump(10);Check(!scene.AnimationRunning&&scene.FrameIndex==0,"모션 줄이기 정지 프레임");f.Dispose();Check(scene.IsDisposed,"입체 자산 컨트롤 정리");}MotionPreferences.UserReducedMotion=false;
#endif
    }
}
