using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using BunjangAuto;
using Lumivolt.Admin;
using Lumivolt.Security;

namespace ExpResetTool {
    static class ResetProgram {
        [STAThread] static int Main(string[] args) {
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
                using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")) {
                    if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}
                }
            };
            return Run(args);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int Run(string[] args) {
            if(args.Length==2 && args[0]=="--worker")return Broker.Worker(args[1]);
            if(args.Length==3 && (args[0]=="--reset-owner" || args[0]=="--reset-client")) {
                try { File.WriteAllText(args[2],Broker.Execute(args[1],args[0]=="--reset-owner").ToString());return 0; }
                catch(Exception ex){File.WriteAllText(args[2],ex.ToString());return 1;}
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppFont.Init();
            if(args.Length==2 && args[0]=="--render") {
                Directory.CreateDirectory(args[1]);
                foreach(bool dark in new[]{false,true})using(var f=new ResetForm()) {
                    f.SetMode(dark,false);f.Show();Application.DoEvents();
                    using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,f.Size));bmp.Save(Path.Combine(args[1],dark?"EXP-Reset-Dark.png":"EXP-Reset-Light.png"));}
                    f.Close();
                }
                return 0;
            }
            if(args.Length!=0)return 2;
            Application.Run(new ResetForm());return 0;
        }
    }
    // Always run destructive work via an interactive Windows task, never an inherited
    // packaged process registry view. The worker independently verifies issuer ownership.
    static class Broker {
        internal static string Base {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EXPReset");}}
        static string JobPath(string id){Guid.ParseExact(id,"N");return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".EXP-Reset-runs",id+".json");}
        internal static JObject Execute(string vault,bool history) {
            ResetEngine.Authorize(vault);
            string id=Guid.NewGuid().ToString("N"), job=JobPath(id), result=job+".result", name="EXP-Reset-"+id;
            Directory.CreateDirectory(Path.GetDirectoryName(job));
            File.WriteAllText(job,new JObject{{"vault",Path.GetFullPath(vault)},{"history",history},{"user",WindowsIdentity.GetCurrent().User.Value}}.ToString());
            dynamic service=null,folder=null,registered=null;
            try {
                service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));service.Connect();folder=service.GetFolder("\\");
                dynamic definition=service.NewTask(0);
                definition.RegistrationInfo.Description="EXP 인증 초기화 · 실행 후 자동 제거";
                definition.Principal.UserId=WindowsIdentity.GetCurrent().Name;definition.Principal.LogonType=3;definition.Principal.RunLevel=0;
                definition.Settings.Enabled=true;definition.Settings.Hidden=true;definition.Settings.AllowDemandStart=true;
                definition.Settings.DisallowStartIfOnBatteries=false;definition.Settings.StopIfGoingOnBatteries=false;definition.Settings.ExecutionTimeLimit="PT0S";
                dynamic action=definition.Actions.Create(0);action.Path=Assembly.GetExecutingAssembly().Location;action.Arguments="--worker "+id;
                registered=folder.RegisterTaskDefinition(name,definition,6,definition.Principal.UserId,null,3,null);registered.Run(null);
                for(int i=0;i<600;i++) {
                    if(File.Exists(result)){var data=JObject.Parse(File.ReadAllText(result));if(!(bool)data["success"])throw new IOException((string)data["message"]);return data;}
                    Thread.Sleep(200);
                }
                throw new IOException("초기화 응답이 지연되고 있습니다. 실행 중인 EXP를 닫고 상태를 확인해 주세요.");
            } finally {
                bool running=false;try{running=registered!=null && (int)registered.State==4;}catch{}
                if(!running) {
                    try{if(folder!=null)folder.DeleteTask(name,0);}catch{}
                    try{if(File.Exists(job))File.Delete(job);if(File.Exists(result))File.Delete(result);if(Directory.GetFileSystemEntries(Path.GetDirectoryName(job)).Length==0)Directory.Delete(Path.GetDirectoryName(job));}catch{}
                }
            }
        }
        internal static int Worker(string id) {
            string job;try{job=JobPath(id);}catch{return 2;}
            JObject result;
            try {
                var data=JObject.Parse(File.ReadAllText(job));
                if((string)data["user"]!=WindowsIdentity.GetCurrent().User.Value)throw new IOException("요청한 Windows 사용자와 다릅니다.");
                result=ResetEngine.Reset((string)data["vault"],(bool)data["history"]);
            }catch(Exception ex){result=new JObject{{"success",false},{"message",ex.Message}};}
            string temp=job+".result.tmp";File.WriteAllText(temp,result.ToString());File.Move(temp,job+".result");
            try{dynamic scheduler=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));scheduler.Connect();scheduler.GetFolder("\\").DeleteTask("EXP-Reset-"+id,0);}catch{}
            return (bool)result["success"]?0:1;
        }
    }
    static class ResetEngine {
#if TESTBUILD
        internal static string DataRoot {get{return Paths.Root;}}
        internal static string UsageRegistry {get{return @"Software\EXP\ResetTests\"+Process.GetCurrentProcess().Id;}}
        internal static string IssuerRegistry {get{return @"Software\EXP\ResetIssuerTests\"+Process.GetCurrentProcess().Id;}}
        internal static string[] DeviceKeys {get{return new[]{"EXP.ResetTest.Device."+Process.GetCurrentProcess().Id,"EXP.ResetTest.Usage."+Process.GetCurrentProcess().Id};}}
        internal static CngProvider KeyProvider {get{return CngProvider.MicrosoftSoftwareKeyStorageProvider;}}
#else
        internal static string DataRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"BunjangAuto");}}
        internal static string UsageRegistry {get{return @"Software\EXP\Usage";}}
        internal static string IssuerRegistry {get{return @"Software\Lumivolt\Issuer";}}
        internal static string[] DeviceKeys {get{return new[]{"Lumivolt.Device.v3","EXP.Usage.v1"};}}
        internal static CngProvider KeyProvider {get{return new CngProvider("Microsoft Platform Crypto Provider");}}
#endif
        internal static void Authorize(string vault) {
            try {
                var key=IssuerService.Load(vault).GeneratePublicKey().GetEncoded();byte[] pinned;
                if(!TrustedKeys.All.TryGetValue(LicenseProtocol.KeyId(key),out pinned) || !key.SequenceEqual(pinned))throw new Exception();
            }catch{throw new InvalidOperationException("EXP Master의 발급 보관소를 선택해 주세요. 발급 보안키를 보유한 관리자만 초기화할 수 있습니다.");}
        }
        internal static string FindVault() {
            var candidates=new List<string>();try{candidates.Add(IssuerService.LastVault);}catch{}
            foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})
                if(Directory.Exists(root))candidates.AddRange(Directory.GetDirectories(root,"EXP_MasterVault*"));
            foreach(var p in candidates)try{Authorize(p);return p;}catch{}
            return "";
        }
        static string Hash(string path){using(var sha=SHA256.Create())using(var s=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(s));}
        internal static void SafeTree(string path,string root) {
            string full=Path.GetFullPath(path), prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("초기화 경로가 허용 범위를 벗어났습니다.");
            if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("연결된 폴더는 초기화할 수 없습니다: "+Path.GetFileName(full));
            if(Directory.Exists(full))foreach(var child in Directory.GetFileSystemEntries(full))SafeTree(child,root);
        }
        static void Remove(string path){if(Directory.Exists(path))Directory.Delete(path,true);else if(File.Exists(path))File.Delete(path);}
        internal static JObject Reset(string vault,bool history) {
            Authorize(vault);vault=Path.GetFullPath(vault);
            var active=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"EXP","EXP Classic","EXP Studio","EXP Pro","EXP-Master","Lumivolt"};
            foreach(var process in Process.GetProcesses())using(process){try{if(active.Contains(process.ProcessName))throw new IOException("EXP와 EXP Master를 모두 닫은 뒤 다시 시도해 주세요.");}catch(InvalidOperationException){}}
            using(var usage=new Mutex(false,"Local\\EXP_Usage_v1"))using(var app=new Mutex(false,"BunjangAuto_SingleInstance"))using(var trials=new Mutex(false,"Local\\EXP_Trial_Issuer")) {
                bool u=false,a=false,t=false;
                try {
                    u=Take(usage);a=Take(app);t=Take(trials);if(!u||!a||!t)throw new IOException("EXP 작업이 진행 중입니다. 프로그램을 닫고 다시 시도해 주세요.");
                    string root=Path.GetFullPath(DataRoot);
                    if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("데이터 폴더가 연결 경로입니다. 초기화를 중단했습니다.");
                    var keep=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var targets=new List<string>();
                    if(Directory.Exists(root))foreach(var p in Directory.GetFileSystemEntries(root)){
                        SafeTree(p,root);string name=Path.GetFileName(p);
                        if(name.Equals("products.json",StringComparison.OrdinalIgnoreCase)||name.Equals("photos",StringComparison.OrdinalIgnoreCase)) {
                            if(File.Exists(p))keep[p]=Hash(p);else foreach(var f in Directory.GetFiles(p,"*",SearchOption.AllDirectories))keep[f]=Hash(f);
                        }else targets.Add(p);
                    }
                    var issuerFiles=new[]{Path.Combine(vault,"issuer.dpapi"),Path.Combine(vault,"issuer-public.json")};
                    foreach(var f in issuerFiles)if(File.Exists(f))keep[f]=Hash(f);
                    if(history)foreach(var name in new[]{"records","credits"}){string p=Path.Combine(vault,name);if(Directory.Exists(p)){SafeTree(p,vault);targets.Add(p);}}
                    // All path and issuer checks complete before the first deletion.
                    foreach(var p in targets)foreach(var file in Directory.Exists(p)?Directory.GetFiles(p,"*",SearchOption.AllDirectories):new[]{p})
                        using(var check=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                    foreach(var p in targets)Remove(p);
                    Registry.CurrentUser.DeleteSubKeyTree(UsageRegistry,false);
                    if(history)Registry.CurrentUser.DeleteSubKeyTree(IssuerRegistry,false);
                    var provider=KeyProvider;
                    foreach(var name in DeviceKeys)if(CngKey.Exists(name,provider))using(var key=CngKey.Open(name,provider))key.Delete();
                    foreach(var pair in keep)if(!File.Exists(pair.Key)||Hash(pair.Key)!=pair.Value)throw new IOException("상품·사진 또는 발급 보안키 보존 확인에 실패했습니다.");
                    using(var k=Registry.CurrentUser.OpenSubKey(UsageRegistry))if(k!=null)throw new IOException("사용량 레지스트리가 남아 있습니다.");
                    foreach(var name in DeviceKeys)if(CngKey.Exists(name,provider))throw new IOException("기기 인증 키가 남아 있습니다.");
                    if(Directory.Exists(root))foreach(var p in Directory.GetFileSystemEntries(root))if(!new[]{"products.json","photos"}.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase))throw new IOException("초기화 대상 파일이 남아 있습니다.");
                    return new JObject{{"success",true},{"message","새 등록코드로 다시 시작할 준비가 됐습니다."},{"preservedFiles",keep.Count},{"masterHistoryReset",history},{"utc",DateTime.UtcNow.ToString("o")}};
                }finally{if(t)trials.ReleaseMutex();if(a)app.ReleaseMutex();if(u)usage.ReleaseMutex();}
            }
        }
        static bool Take(Mutex m){try{return m.WaitOne(0);}catch(AbandonedMutexException){return true;}}
    }
    sealed class StatusDot : Control,IThemed {
        internal int State;internal StatusDot(){DoubleBuffered=true;Size=new Size(30,30);}
        public void ApplyTheme(){Invalidate();}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;var color=State==1?Theme.Success:State==2?Theme.Danger:Theme.Accent;using(var b=new SolidBrush(color))e.Graphics.FillEllipse(b,2,2,Width-4,Height-4);using(var pen=new Pen(Color.White,2.2f)){if(State==1)e.Graphics.DrawLines(pen,new[]{new Point(8,15),new Point(13,20),new Point(23,10)});else if(State==2){e.Graphics.DrawLine(pen,10,10,20,20);e.Graphics.DrawLine(pen,20,10,10,20);}else e.Graphics.DrawLine(pen,9,15,21,15);}}
    }
    sealed class ResetForm : Form {
        readonly SoftInput vault;readonly SoftButton start,light,dark;readonly CheckBox history;readonly Label status;readonly StatusDot[] dots=new StatusDot[3];bool busy;
        public ResetForm() {
            Text="EXP Reset";ClientSize=new Size(780,770);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;Font=Theme.FontBase();
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.app.ico"))if(s!=null)Icon=new Icon(s);
            Controls.Add(new HeroBanner{Left=28,Top=24,Width=724,Height=136,Eyebrow="EXP RESET  /  OWNER",Title="설정 초기화",Subtitle="상품과 사진은 그대로. 인증과 사용 설정을 새롭게."});
            Controls.Add(Design.Label("초기화할 항목",32,188,410,28,15,"t"));
            light=Design.Button("라이트",556,184,94,false);dark=Design.Button("다크",658,184,94,false);light.Click+=delegate{SetMode(false,true);};dark.Click+=delegate{SetMode(true,true);};Controls.AddRange(new Control[]{light,dark});
            string[] titles={"인증키 · 기기 등록","체험 · 사용량 · 로그인 설정","상품 목록 · 사진"};
            string[] descriptions={"현재 인증을 지우고 새 등록코드로 시작합니다.","이 PC의 사용 기록과 앱 설정을 초기화합니다.","작성한 상품과 사진은 그대로 보존합니다."};
            for(int i=0;i<3;i++){var card=new SoftCard{Left=28,Top=236+i*86,Width=724,Height=76};dots[i]=new StatusDot{Left=20,Top=22};card.Controls.Add(dots[i]);card.Controls.Add(Design.Label(titles[i],66,12,610,26,12,"t"));card.Controls.Add(Design.Label(descriptions[i],66,42,610,22,10,"m"));Controls.Add(card);}
            var owner=new SoftCard{Left=28,Top=510,Width=724,Height=146};owner.Controls.Add(Design.Label("EXP Master 발급 보관소",20,12,630,24,11,"t"));
            vault=Design.Input("vault",20,44,564,38,true);vault.Multiline=false;vault.Text=ResetEngine.FindVault();owner.Controls.Add(vault);
            var browse=Design.Button("찾아보기",594,44,108,false);browse.Click+=delegate{using(var d=new FolderBrowserDialog{Description="EXP Master의 발급 보안키가 있는 폴더"})if(d.ShowDialog(this)==DialogResult.OK)vault.Text=d.SelectedPath;};owner.Controls.Add(browse);
            history=new CheckBox{Left=22,Top=99,Width=668,Height=28,Text="Master 발급 이력도 초기화 · 발급 보안키는 유지",Font=Theme.Font(10,FontStyle.Bold),Checked=false};owner.Controls.Add(history);Controls.Add(owner);
            status=Design.Label("관리자 전용 · 고객에게 전달하지 마세요.",32,686,490,54,10,"m");Controls.Add(status);
            start=Design.Button("초기화",562,690,190,true);start.Height=44;start.Click+=async delegate{await Reset();};Controls.Add(start);
            bool isDark=false;try{isDark=File.ReadAllText(Path.Combine(Broker.Base,"theme.txt")).Trim()=="Dark";}catch{}SetMode(isDark,false);
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};Design.Scale(this);Theme.Apply(this);
        }
        internal void SetMode(bool isDark,bool save){Theme.Set(isDark?ThemePalette.DarkPal:ThemePalette.Light);light.Primary=!isDark;dark.Primary=isDark;Theme.Apply(this);if(save)try{Directory.CreateDirectory(Broker.Base);File.WriteAllText(Path.Combine(Broker.Base,"theme.txt"),isDark?"Dark":"Light");}catch{}}
        async Task Reset(){
            if(busy)return;try{ResetEngine.Authorize(vault.Text);}catch(Exception ex){AppDialog.Show(this,ex.Message,"EXP Reset");return;}
            string message="현재 인증키와 기기 등록, 체험·사용량·로그인 설정을 삭제합니다.\r\n상품과 사진, 관리자 발급 보안키는 보존합니다.\r\n"+(history.Checked?"Master 발급 이력도 삭제합니다.\r\n":"")+"초기화 후에는 새 등록코드로 키를 발급해야 합니다.\r\n초기화할까요?";
            if(AppDialog.Show(this,message,"EXP Reset",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            busy=true;start.Enabled=false;history.Enabled=false;status.Text="인증 정보를 초기화하고 있습니다…";string path=vault.Text;bool clearHistory=history.Checked;
            try{await Task.Run(()=>Broker.Execute(path,clearHistory));foreach(var dot in dots){dot.State=1;dot.Invalidate();}status.Text="초기화 완료\r\n설치 파일에서 새 등록코드를 만들어 주세요.";start.Text="완료";}
            catch(Exception ex){foreach(var dot in dots){dot.State=2;dot.Invalidate();}status.Text="초기화를 완료하지 못했습니다.";AppDialog.Show(this,ex.Message,"EXP Reset");start.Enabled=true;}
            finally{busy=false;history.Enabled=true;}
        }
    }
}
