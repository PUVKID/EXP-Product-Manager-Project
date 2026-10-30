using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BunjangAuto
{
    static class Program
    {
        // exe 안에 내장된 DLL(Selenium 등)을 꺼내 쓰기 위한 처리 → exe 파일 하나만 있으면 실행된다
        private static readonly Dictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();

        private static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            string simple = new AssemblyName(args.Name).Name;
            lock (_cache)
            {
                Assembly cached;
                if (_cache.TryGetValue(simple, out cached)) return cached;
                var self = Assembly.GetExecutingAssembly();
                string res = "lib." + simple + ".dll";
                using (Stream s = self.GetManifestResourceStream(res))
                {
                    if (s == null) return null;
                    byte[] data = new byte[s.Length];
                    int read = 0;
                    while (read < data.Length) { int n = s.Read(data, read, data.Length - read); if (n <= 0) break; read += n; }
                    var asm = Assembly.Load(data);
                    _cache[simple] = asm;
                    return asm;
                }
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

        /// <summary>고DPI(125~200%)에서 흐릿하게 늘어나지 않도록 창을 만들기 전에 DPI 인식을 켠다. (매니페스트와 같은 효과 — 매니페스트가 없는 빌드에서도 동작)</summary>
        private static void EnableDpiAwareness()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try { SetProcessDPIAware(); }   // 시스템 DPI 인식: WinForms 레이아웃을 가장 예측 가능하게 키울 수 있는 방식
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            EnableDpiAwareness();
            AppFont.Init();   // 글꼴 객체가 만들어지기 전에 내장 나눔고딕 Bold 를 등록
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
#if TESTBUILD
            if (args.Length > 0 && args[0] == "--selftest") { Environment.ExitCode = SelfTest.Run(); return; }
#endif
            if (args.Length == 1 && (args[0] == "--activate-for-setup" || args[0] == "--check-for-setup")) { Environment.ExitCode = SetupActivation(args[0] == "--check-for-setup"); return; }
            RunApp();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int SetupActivation(bool silent)
        {
            try {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Paths.EnsureAll(); try { ThemeService.Init(Storage.LoadSettings()); } catch { }
                if (!(silent ? LicenseGate.Recheck() : LicenseGate.Check())) return 1;
                UsageQuota.EnsureReady();
                return Lumivolt.Security.EditionPolicy.SetupCode(LicenseGate.LastInfo.Edition);
            } catch (Exception ex) { try { File.AppendAllText(Path.Combine(Paths.Root,"activation-diagnostic.txt"),DateTime.UtcNow.ToString("o")+" | setup error | "+ex.ToString()+Environment.NewLine); } catch { } if (!silent) AppDialog.Show(ex.Message,"EXP 인증"); return 1; }
        }

        // Selenium 등 내장 DLL 을 쓰는 코드는 별도 메서드에 둬서, 리졸버 등록 이후에 JIT 되도록 한다
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void RunApp()
        {
            bool created;
            using (var mutex = new Mutex(true, "BunjangAuto_SingleInstance", out created))
            {
                if (!created)
                {
                    AppDialog.Show("이미 실행 중입니다.", "EXP", MessageBoxButtons.OK, MessageBoxIcon.Information);   // 아직 아무것도 시작하지 않은 상태라 기본 상자를 쓴다
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Paths.EnsureAll();
                CrashReportService.Install();
                Log.AppVersion = Brand.Version;
                // 테마는 첫 화면(약관/라이선스 창)부터 적용한다. 저장된 선택(settings.json)을 읽어 온다.
                try { ThemeService.Init(Storage.LoadSettings()); } catch { }
                if (Guard.Suspicious()) { Guard.Die("분석/디버깅 도구가 감지되어 프로그램을 종료합니다.\r\n(저작권자: EXPMASTER)"); return; }
                if (!TermsGate.Check()) return;
                if (!LicenseGate.Check()) return;
                // Recent activity counters are local to this PC.
                UsageMetricsService.Load();
                UsageMetricsService.Hook();
                Log.Write("INFO", "App", "app.start", "프로그램 v" + Brand.Version + " 실행 (" + Environment.OSVersion.VersionString + ")", "OK");
                Application.ThreadException += (s, e) => CrashReportService.ReportUiException(e.Exception);
                try { Application.Run(new MainForm()); }
                finally
                {
                    Log.Write("INFO", "App", "app.exit", "프로그램을 종료합니다.", "OK");
                }
            }
        }
    }
}
