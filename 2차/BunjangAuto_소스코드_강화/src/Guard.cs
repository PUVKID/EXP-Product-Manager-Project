using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>디버거/분석 도구가 감지되면 즉시 종료한다.</summary>
    public static class Guard
    {
        [DllImport("kernel32.dll")] private static extern bool IsDebuggerPresent();
        [DllImport("kernel32.dll")] private static extern bool CheckRemoteDebuggerPresent(IntPtr h, ref bool present);

        private static readonly string[] Bad = new string[]
        {
            "dnspy", "dnspy-x86", "ilspy", "dotpeek", "dotpeek64", "de4dot", "x64dbg", "x32dbg", "ollydbg",
            "cheatengine-x86_64", "cheatengine-i386", "processhacker", "processhacker2", "reflector",
            "megadumper", "scylla", "ida", "ida64", "fiddler", "wireshark", "procmon", "procmon64",
            "procexp", "procexp64", "httpdebugger", "charles", "pestudio", "exeinfope", "detectiteasy",
            "die", "resourcehacker", "hxd", "cff explorer", "dnspy-x86-netcore", "dnspy-netcore"
        };

        // 타이밍 체크: 디버거가 중단점을 걸고 있으면 두 시각 사이 간격이 비정상적으로 벌어진다.
        private static bool TimingAnomaly()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                int x = 0;
                for (int i = 0; i < 1000; i++) x += i;
                sw.Stop();
                return sw.ElapsedMilliseconds > 200; // 정상 실행이면 수 ms 이내
            }
            catch { return false; }
        }

        public static bool Suspicious()
        {
            try
            {
                if (Debugger.IsAttached) return true;
                if (IsDebuggerPresent()) return true;
                bool r = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref r);
                if (r) return true;
                if (TimingAnomaly()) return true;
                foreach (var p in Process.GetProcesses())
                {
                    string n = "";
                    try { n = p.ProcessName.ToLowerInvariant(); } catch { }
                    foreach (var b in Bad) if (n.Contains(b)) return true;
                }
            }
            catch { }
            return false;
        }

        public static void Die(string msg)
        {
            try { MessageBox.Show(msg, "번개장터 자동등록", MessageBoxButtons.OK, MessageBoxIcon.Stop); } catch { }
            Environment.Exit(1);
        }

        /// <summary>디버거/분석 도구 + 라이선스 재확인. 문제가 있으면 종료.</summary>
        public static void Check()
        {
            if (Suspicious()) Die("분석/디버깅 도구가 감지되어 프로그램을 종료합니다.\r\n(저작권자: 음악천재)");
            if (!LicenseGate.Recheck()) Die("라이선스를 확인할 수 없어 프로그램을 종료합니다.");
        }
    }
}
