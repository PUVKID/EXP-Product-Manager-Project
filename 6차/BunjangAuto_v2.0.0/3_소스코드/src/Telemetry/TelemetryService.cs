using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BunjangAuto
{
    /// <summary>
    /// 로컬 로그(Log) ↔ 중앙 서버를 잇는 얇은 계층.
    ///   Log.Entry → LogQueue → CentralLogService(백그라운드, HTTPS 배치) → 서버
    /// 서버 주소/활성화는 %AppData%\BunjangAuto\telemetry.json 에서 바꾼다. 전송이 실패해도 프로그램 기능에는 영향이 없다.
    /// </summary>
    public static class TelemetryService
    {
        public static TelemetryConfig Config;
        public static LogQueue Queue;
        public static CentralLogService Central;
        private static Timer _heartbeat;
        private static bool _started;

        public static bool Active { get { return Config != null && Config.Active; } }

        /// <summary>앱 시작 시 한 번 호출. 설정이 꺼져 있거나 서버 주소가 없으면 전송 없이 조용히 끝난다.</summary>
        public static void Start(ITransport transport = null)
        {
            if (_started) return;
            _started = true;
            try
            {
                Log.AppVersion = ClientIdentityService.AppVersion;
                Config = TelemetryConfig.Load();
                Queue = new LogQueue(Path.Combine(Paths.Root, "telemetry-queue.jsonl"));
                if (!Config.Active) return;
                Queue.Restore();
                Central = new CentralLogService(Config, Queue, transport);
                Log.Entry += OnEntry;
                Central.Start();
                _heartbeat = new Timer(delegate { Heartbeat(); }, null, Config.HeartbeatSeconds * 1000, Config.HeartbeatSeconds * 1000);
            }
            catch { }
        }

        private static void OnEntry(LogEntry e)
        {
            try
            {
                if (Queue == null) return;
                Queue.Enqueue(e);
                if (Queue.Count >= Config.BatchSize && Central != null) Central.Poke();
            }
            catch { }
        }

        /// <summary>접속 중임을 알리는 신호. 로컬 로그 파일에는 남기지 않고 큐에만 넣는다.</summary>
        private static void Heartbeat()
        {
            try
            {
                if (Queue == null) return;
                Queue.Enqueue(new LogEntry { Level = "DEBUG", Module = "Telemetry", Action = "heartbeat", Message = "heartbeat", Version = Log.AppVersion, Session = Log.SessionId });
                if (Central != null) Central.Poke();
            }
            catch { }
        }

        /// <summary>구조화 이벤트를 남기는 편의 함수. 로컬 로그에도 그대로 기록된다.</summary>
        public static void Track(string action, string module, string message, string level = "INFO", string result = "")
        {
            Log.Write(level, module, action, message, result);
        }

        /// <summary>종료 직전/크래시 직전에 남은 로그를 최대 waitMs 동안 보내 본다. 못 보내면 디스크에 보관한다.</summary>
        public static void FlushNow(int waitMs)
        {
            try
            {
                if (Central == null || Queue == null) return;
                var t = new Thread(delegate () { try { while (Queue.Count > 0 && Central.FlushOnce()) { } } catch { } });
                t.IsBackground = true; t.Start();
                if (!t.Join(waitMs)) { }
                Queue.Persist();
            }
            catch { }
        }

        public static void Shutdown()
        {
            try
            {
                if (_heartbeat != null) { _heartbeat.Dispose(); _heartbeat = null; }
                FlushNow(2500);
                if (Central != null) Central.Stop(500);
                if (Queue != null) Queue.Persist();
            }
            catch { }
        }
    }
}
