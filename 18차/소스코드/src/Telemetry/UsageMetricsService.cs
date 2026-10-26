using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>이 PC 안에서만 쓰는 사용량 집계(프로그램 실행 횟수, 등록 성공/실패 누적). Dashboard 의 "최근 활동"에 쓰인다.</summary>
    public class UsageMetrics
    {
        public int Launches;
        public int Registrations;
        public int Success;
        public int Fail;
        public int Errors;
        public string LastStart = "";
        public string LastSuccess = "";
        public string LastFail = "";
        public string LastError = "";
        public string LastRun = "";
    }

    public static class UsageMetricsService
    {
        private static readonly object _gate = new object();
        public static UsageMetrics Data = new UsageMetrics();
        public static event Action Changed;

        private static string FilePath { get { return Path.Combine(Paths.Root, "usage.dat"); } }

        public static void Load()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var j = JsonConvert.DeserializeObject<UsageMetrics>(Vault.Unprotect(File.ReadAllText(FilePath, Encoding.UTF8), "usage"));
                        if (j != null) Data = j;
                    }
                }
                catch { Data = new UsageMetrics(); }
            }
        }

        private static void Save()
        {
            try { File.WriteAllText(FilePath, Vault.Protect(JsonConvert.SerializeObject(Data), "usage"), Encoding.UTF8); } catch { }
        }

        public static void Hook() { Log.Entry += OnEntry; }

        private static string Now() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); }

        public static void OnEntry(LogEntry e)
        {
            bool changed = true;
            lock (_gate)
            {
                switch (e.Action)
                {
                    case "app.start": Data.Launches++; Data.LastStart = Now(); break;
                    case "product.register.start": Data.Registrations++; Data.LastRun = Now(); break;
                    case "product.register.success": Data.Success++; Data.LastSuccess = Now(); break;
                    case "product.register.fail": case "image.upload.fail": case "category.fail": Data.Fail++; Data.LastFail = Now(); break;
                    default: changed = false; break;
                }
                if (e.Level == "ERROR" || e.Level == "FATAL") { Data.Errors++; Data.LastError = Now(); changed = true; }
                if (changed) Save();
            }
            if (changed) { var h = Changed; if (h != null) try { h(); } catch { } }
        }
    }
}
