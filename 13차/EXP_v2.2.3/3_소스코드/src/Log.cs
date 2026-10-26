using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>구조화된 로그 한 건 (로컬 Log Viewer 와 중앙 서버 전송에서 같은 형식을 쓴다).</summary>
    public class LogEntry
    {
        [JsonProperty("id")] public string Id = Guid.NewGuid().ToString("N");
        /// <summary>UTC ISO-8601 (예: 2026-10-07T01:02:03.456Z)</summary>
        [JsonProperty("ts")] public string Ts = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        [JsonProperty("level")] public string Level = "INFO";
        [JsonProperty("module")] public string Module = "App";
        [JsonProperty("action")] public string Action = "log";
        [JsonProperty("message")] public string Message = "";
        [JsonProperty("result")] public string Result = "";
        [JsonProperty("exc_type")] public string ExcType = "";
        [JsonProperty("version")] public string Version = "";
        [JsonProperty("session")] public string Session = "";

        [JsonIgnore]
        public DateTime LocalTime
        {
            get
            {
                DateTime d;
                if (DateTime.TryParse(Ts, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out d))
                    return d.ToLocalTime();
                return DateTime.Now;
            }
        }
    }

    /// <summary>
    /// 화면 + 파일 양쪽에 남기는 로거.
    ///  - Log.Write(string) / Log.Line : 예전 방식 그대로(문구·파일 형식 동일: %AppData%\BunjangAuto\logs\yyyyMMdd.log)
    ///  - 같은 호출이 구조화된 LogEntry 로도 변환되어 Log.Entry 이벤트(Log Viewer, 중앙 로그 큐)와
    ///    logs\yyyyMMdd.jsonl 에 함께 기록된다. 날짜별 rolling.
    /// </summary>
    public static class Log
    {
        public static event Action<string> Line;
        public static event Action<LogEntry> Entry;
        private static readonly object _lock = new object();

        /// <summary>앱이 세션마다 한 번 정하는 값(중앙 서버에서 같은 실행의 로그를 묶어 본다).</summary>
        public static string SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
        public static string AppVersion = "";

        public static void Write(string msg)
        {
            var e = LogClassifier.Classify(msg);
            Emit(msg, e);
        }

        /// <summary>구조화 로그. message 는 기존처럼 화면/파일에 한 줄로 남는다.</summary>
        public static void Write(string level, string module, string action, string msg, string result, string excType = "")
        {
            var e = new LogEntry { Level = level, Module = module, Action = action, Result = result ?? "", ExcType = excType ?? "" };
            Emit(msg, e);
        }

        public static void Error(string module, string action, string msg, Exception ex)
        {
            var e = new LogEntry { Level = "ERROR", Module = module, Action = action, Result = "FAIL", ExcType = ex != null ? ex.GetType().FullName : "" };
            Emit(msg, e);
        }

        private static void Emit(string msg, LogEntry e)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
            e.Message = msg ?? "";
            e.Version = AppVersion;
            e.Session = SessionId;
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Paths.Logs);
                    string day = DateTime.Now.ToString("yyyyMMdd");
                    File.AppendAllText(Path.Combine(Paths.Logs, day + ".log"), line + Environment.NewLine, Encoding.UTF8);
                    File.AppendAllText(Path.Combine(Paths.Logs, day + ".jsonl"), JsonConvert.SerializeObject(e) + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
            try { var h = Line; if (h != null) h(line); } catch { }
            try { var h2 = Entry; if (h2 != null) h2(e); } catch { }
        }

        /// <summary>오늘(및 필요하면 이전 날짜)의 jsonl 에서 최근 n건을 읽는다. Log Viewer 가 시작할 때 과거 기록을 채우는 데 쓴다.</summary>
        public static List<LogEntry> LoadRecent(int max)
        {
            var list = new List<LogEntry>();
            try
            {
                var files = new List<string>(Directory.GetFiles(Paths.Logs, "*.jsonl"));
                files.Sort(StringComparer.Ordinal);
                for (int i = files.Count - 1; i >= 0 && list.Count < max; i--)
                {
                    var lines = File.ReadAllLines(files[i], Encoding.UTF8);
                    for (int k = lines.Length - 1; k >= 0 && list.Count < max; k--)
                    {
                        try { var e = JsonConvert.DeserializeObject<LogEntry>(lines[k]); if (e != null) list.Add(e); } catch { }
                    }
                }
                list.Reverse();
            }
            catch { }
            return list;
        }
    }
}
