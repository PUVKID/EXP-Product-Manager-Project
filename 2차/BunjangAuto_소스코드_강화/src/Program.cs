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

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
            if (args.Length > 0 && args[0] == "--selftest") { Environment.ExitCode = SelfTest.Run(); return; }
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
                    MessageBox.Show("이미 실행 중입니다.", "번개장터 자동등록", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Paths.EnsureAll();
                if (Guard.Suspicious()) { Guard.Die("분석/디버깅 도구가 감지되어 프로그램을 종료합니다.\r\n(저작권자: 음악천재)"); return; }
                if (!TermsGate.Check()) return;
                if (!LicenseGate.Check()) return;
                Application.ThreadException += (s, e) => Log.Write("예외: " + e.Exception.Message);
                Application.Run(new MainForm());
            }
        }
    }
}
