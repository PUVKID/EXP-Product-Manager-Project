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
            if (args.Length > 2 && args[0] == "--selftest-central") { Environment.ExitCode = SelfTestV2.RunCentral(args[1], args[2]); return; }
#endif
#if UIPREVIEW
            if (args.Length > 0 && args[0] == "--ui-preview") { PreviewHarness.Run(args); return; }
            if (args.Length > 0 && args[0] == "--ui-layout") { Environment.ExitCode = UiLayoutCheck.Run(args); return; }
            if (args.Length > 0 && args[0] == "--ui-selftest") { Environment.ExitCode = UiSelfTest.Run(); return; }
#endif
            RunApp();
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
                    MessageBox.Show("이미 실행 중입니다.", "Lumivolt", MessageBoxButtons.OK, MessageBoxIcon.Information);   // 아직 아무것도 시작하지 않은 상태라 기본 상자를 쓴다
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Paths.EnsureAll();
                CrashReportService.Install();
                Log.AppVersion = ClientIdentityService.AppVersion;
                // 테마는 첫 화면(약관/라이선스 창)부터 적용한다. 저장된 선택(settings.json)을 읽어 온다.
                try { ThemeService.Init(Storage.LoadSettings()); } catch { }
                if (Guard.Suspicious()) { Guard.Die("분석/디버깅 도구가 감지되어 프로그램을 종료합니다.\r\n(저작권자: 음악천재)"); return; }
                if (!TermsGate.Check()) return;
                if (!LicenseGate.Check()) return;
                // 약관에 동의하고 라이선스가 확인된 뒤에만 운영 로그 전송을 시작한다.
                UsageMetricsService.Load();
                UsageMetricsService.Hook();
                TelemetryService.Start();
                Log.Write("INFO", "App", "app.start", "프로그램 v" + ClientIdentityService.AppVersion + " 실행 (" + ClientIdentityService.OsDescription() + ")", "OK");
                Application.ThreadException += (s, e) => CrashReportService.ReportUiException(e.Exception);
                try { Application.Run(new MainForm()); }
                finally
                {
                    Log.Write("INFO", "App", "app.exit", "프로그램을 종료합니다.", "OK");
                    TelemetryService.Shutdown();
                }
            }
        }
    }
}
