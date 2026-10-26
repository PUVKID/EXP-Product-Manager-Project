using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>처리되지 않은 예외를 로컬 crash 파일 + 구조화 로그(FATAL)로 남기고, 가능하면 종료 전에 서버로 마지막 전송을 시도한다.</summary>
    public static class CrashReportService
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Report(e.ExceptionObject as Exception, e.IsTerminating);
            };
        }

        /// <summary>UI 스레드 예외(Application.ThreadException)는 프로그램을 계속 쓸 수 있으므로 ERROR 로만 남긴다. 문구는 기존("예외: ...")과 같다.</summary>
        public static void ReportUiException(Exception ex)
        {
            Log.Error("App", "exception", "예외: " + ex.Message, ex);
        }

        public static void Report(Exception ex, bool terminating)
        {
            try
            {
                string type = ex != null ? ex.GetType().FullName : "Unknown";
                string stack = ex != null ? ex.ToString() : "(예외 정보 없음)";
                string name = "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                try
                {
                    Directory.CreateDirectory(Paths.Logs);
                    File.WriteAllText(Path.Combine(Paths.Logs, name),
                        "BunjangAuto " + ClientIdentityService.AppVersion + "\r\n" + DateTime.Now.ToString("o") + "\r\n" + ClientIdentityService.OsDescription() + "\r\n\r\n" + stack, Encoding.UTF8);
                }
                catch { }
                string first = ex != null ? ex.Message : "";
                string msg = "치명적 오류(" + type + "): " + first + "\n" + (stack.Length > 3000 ? stack.Substring(0, 3000) : stack);
                Log.Write("FATAL", "App", "crash", msg, "FAIL", type);
                if (terminating) TelemetryService.FlushNow(2500);
            }
            catch { }
        }
    }
}
