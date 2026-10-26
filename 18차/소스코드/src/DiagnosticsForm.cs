using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace BunjangAuto {
    public enum DiagnosticState { Waiting,Running,Pass,Fail,Skipped }
    public class DiagnosticResult {
        public DiagnosticState State; public string Detail;
        public DiagnosticResult(DiagnosticState state,string detail){State=state;Detail=detail;}
    }
    public class DiagnosticCheck {
        public string Title; public Func<DiagnosticResult> Run;
        public DiagnosticCheck(string title,Func<DiagnosticResult> run){Title=title;Run=run;}
    }
    public static class DiagnosticChecks {
        static DiagnosticResult Pass(string text){return new DiagnosticResult(DiagnosticState.Pass,text);}
        static DiagnosticResult Fail(string text){return new DiagnosticResult(DiagnosticState.Fail,text);}
        public static DiagnosticResult InspectPhotos(string[] paths,int productCount) {
            if(productCount==0)return new DiagnosticResult(DiagnosticState.Skipped,"상품을 추가하면 사진을 확인합니다.");
            if(paths.Length==0)return Fail("상품에 사진을 추가해 주세요.");
            int missing=0;
            foreach(var path in paths)try{using(var image=Image.FromFile(path)){if(image.Width<1 || image.Height<1)missing++;}}catch{missing++;}
            return missing==0?Pass("사진 "+paths.Length+"장을 열 수 있습니다."):Fail("열 수 없는 사진 "+missing+"장 · 상품 편집에서 다시 추가해 주세요.");
        }
        public static DiagnosticCheck[] Create(AppSettings config,IEnumerable<Product> products) {
            string chrome=config.ChromePath;
            var items=products.ToArray();int count=items.Length;
            int withoutPhoto=items.Count(x=>String.IsNullOrWhiteSpace(x.Images));
            var photos=items.SelectMany(x=>(x.Images??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)).Select(x=>x.Trim()).Distinct().ToArray();
            return new[]{
                new DiagnosticCheck("기기 인증",delegate {try{using(var key=TpmDevice.Open(false))TpmDevice.Prove(Convert.ToBase64String(key.Export(System.Security.Cryptography.CngKeyBlobFormat.EccPublicBlob)));return Pass("등록한 기기의 보안키를 확인했습니다.");}catch{return Fail("등록한 Windows 계정으로 실행하고 인증을 확인해 주세요.");}}),
                new DiagnosticCheck("라이선스",delegate {string path=Path.Combine(Paths.Root,"license.key");if(!File.Exists(path))return Fail("라이선스 화면에서 인증키를 입력해 주세요.");var info=License.Verify(Lumivolt.Security.LicenseProtocol.ReadFile(path),"");return info.Valid&&LicenseClock.Accept(false)?Pass("EXP "+info.Edition+" · "+(info.Expires==DateTime.MaxValue?"무기한":info.Expires.ToString("yyyy-MM-dd HH:mm")+"까지")):Fail("인증키 또는 사용 기간을 확인해 주세요.");}),
                new DiagnosticCheck("Chrome",delegate {string exe=DriverManager.FindChromeExe(chrome);return String.IsNullOrEmpty(exe)?Fail("Chrome을 설치하거나 설정에서 실행 파일을 지정해 주세요."):Pass("Chrome "+DriverManager.GetChromeVersion(exe));}),
                new DiagnosticCheck("데이터 저장",delegate {string path=Path.Combine(Paths.Root,"diagnostic-"+Guid.NewGuid().ToString("N")+".tmp");try{Directory.CreateDirectory(Paths.Root);string value=Guid.NewGuid().ToString();File.WriteAllText(path,value);return File.ReadAllText(path)==value?Pass("상품과 설정을 저장할 수 있습니다."):Fail("데이터 폴더의 읽기 권한을 확인해 주세요.");}finally{if(File.Exists(path))File.Delete(path);}}),
                new DiagnosticCheck("상품 사진",delegate {var result=InspectPhotos(photos,count);if(withoutPhoto>0)return Fail("사진이 없는 상품 "+withoutPhoto+"개 · 상품 편집에서 사진을 추가해 주세요.");return result;}),
                new DiagnosticCheck("번개장터 시각 연결",delegate {ServerClock.RefreshAsync();for(int i=0;i<120 && !ServerClock.Fresh;i++)Thread.Sleep(100);return ServerClock.Fresh?Pass("서버 시각과 연결되었습니다."):Fail("인터넷 연결을 확인하고 다시 진단해 주세요.");})
            };
        }
    }
    public class DiagnosticRow : Control,IThemed {
        public string Title="";public DiagnosticResult Result=new DiagnosticResult(DiagnosticState.Waiting,"대기 중");
        public DiagnosticRow(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);TabStop=false;}
        public void ApplyTheme(){Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            var p=Theme.Current;var g=e.Graphics;g.Clear(Parent==null?p.Bg:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Theme.RoundedRect(new Rectangle(0,0,Width-1,Height-1),Dpi.S(16)))using(var fill=new SolidBrush(p.Card))using(var pen=new Pen(p.Border)){g.FillPath(fill,path);g.DrawPath(pen,path);}
            Color color=Result.State==DiagnosticState.Pass?p.Success:Result.State==DiagnosticState.Fail?p.Danger:Result.State==DiagnosticState.Running?p.Accent:p.TextMuted;
            int size=Dpi.S(26),x=Dpi.S(20),y=(Height-size)/2;
            using(var brush=new SolidBrush(color))g.FillEllipse(brush,x,y,size,size);
            using(var pen=new Pen(p.Dark?p.Bg:Color.White,Dpi.S(2))){pen.StartCap=pen.EndCap=LineCap.Round;
                if(Result.State==DiagnosticState.Pass){g.DrawLine(pen,x+size*.25f,y+size*.5f,x+size*.43f,y+size*.7f);g.DrawLine(pen,x+size*.43f,y+size*.7f,x+size*.76f,y+size*.3f);}
                else if(Result.State==DiagnosticState.Fail){g.DrawLine(pen,x+size*.33f,y+size*.33f,x+size*.67f,y+size*.67f);g.DrawLine(pen,x+size*.33f,y+size*.67f,x+size*.67f,y+size*.33f);}
                else g.DrawLine(pen,x+size*.3f,y+size*.5f,x+size*.7f,y+size*.5f);
            }
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g,Title,Theme.Font(11f,FontStyle.Bold),new Rectangle(Dpi.S(62),Dpi.S(12),Width-Dpi.S(180),Dpi.S(26)),p.Text,flags);
            TextRenderer.DrawText(g,Result.Detail,Theme.Font(9.5f,FontStyle.Bold),new Rectangle(Dpi.S(62),Dpi.S(42),Width-Dpi.S(82),Dpi.S(24)),p.TextMuted,flags);
            string label=Result.State==DiagnosticState.Pass?"PASS":Result.State==DiagnosticState.Fail?"FAIL":Result.State==DiagnosticState.Running?"확인 중":Result.State==DiagnosticState.Skipped?"미검사":"대기";
            TextRenderer.DrawText(g,label,Theme.Font(9f,FontStyle.Bold),new Rectangle(Width-Dpi.S(96),Dpi.S(12),Dpi.S(76),Dpi.S(26)),color,flags);
        }
    }
    public class DiagnosticsForm : Form {
        readonly DiagnosticCheck[] checks;readonly DiagnosticRow[] rows;readonly SoftButton run;readonly Label summary;
        volatile bool cancelled;bool running;
        public bool Completed {get;private set;}
        public DiagnosticsForm(AppSettings config,IEnumerable<Product> products) {
            Text="EXP · 자가진단";ClientSize=new Size(780,790);Font=Theme.FontBase();StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            checks=DiagnosticChecks.Create(config,products);rows=new DiagnosticRow[checks.Length];
            Controls.Add(new HeroBanner {Left=24,Top=20,Width=732,Height=136,Eyebrow="EXP / HEALTH CHECK",Title="자가진단",Subtitle="문제가 있는 항목과 해결 방법을 확인하세요."});
            for(int i=0;i<checks.Length;i++){rows[i]=new DiagnosticRow {Name="diagnostic"+i,Left=24,Top=174+i*88,Width=732,Height=78,Title=checks[i].Title};Controls.Add(rows[i]);}
            summary=Design.Label("진단 준비 중",26,728,500,30,11,"m");run=Design.Button("다시 진단",608,724,148,true);Controls.AddRange(new Control[]{summary,run});
            run.Click+=delegate{Start();};Shown+=delegate{Start();};FormClosed+=delegate{cancelled=true;};Design.Scale(this);Theme.Apply(this);
        }
        void Post(Action action){try{if(!cancelled && !IsDisposed && IsHandleCreated)BeginInvoke((Action)delegate{if(!cancelled && !IsDisposed)action();});}catch(InvalidOperationException){}}
        void Start(){
            if(running)return;running=true;Completed=false;run.Enabled=false;summary.Text="0 / "+checks.Length+" 항목 확인";
            foreach(var row in rows){row.Result=new DiagnosticResult(DiagnosticState.Waiting,"대기 중");row.Invalidate();}
            ThreadPool.QueueUserWorkItem(delegate {
                int passed=0,failed=0,skipped=0;
                for(int i=0;i<checks.Length&&!cancelled;i++){
                    int index=i;Post(delegate{rows[index].Result=new DiagnosticResult(DiagnosticState.Running,"확인하고 있습니다.");rows[index].Invalidate();});
                    DiagnosticResult result;try{result=checks[i].Run();}catch{result=new DiagnosticResult(DiagnosticState.Fail,"확인할 수 없습니다. 다시 진단하거나 "+Brand.Email+"로 문의해 주세요.");}
                    if(result.State==DiagnosticState.Pass)passed++;else if(result.State==DiagnosticState.Fail)failed++;else skipped++;
                    Post(delegate{rows[index].Result=result;rows[index].AccessibleName=rows[index].Title+" "+result.State+" "+result.Detail;rows[index].Invalidate();summary.Text=(index+1)+" / "+checks.Length+" 항목 확인";});
                }
                Post(delegate{summary.Text="통과 "+passed+"  ·  확인 필요 "+failed+(skipped>0?"  ·  미검사 "+skipped:"");running=false;Completed=true;run.Enabled=true;});
            });
        }
    }
}
