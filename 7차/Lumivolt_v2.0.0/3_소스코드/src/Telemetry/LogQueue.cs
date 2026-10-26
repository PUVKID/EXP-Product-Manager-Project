using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>
    /// 중앙 서버로 보낼 로그 대기열. 메모리에 쌓고, 전송 실패/종료 시 디스크(telemetry-queue.jsonl)에 보관했다가
    /// 다음 실행에 이어서 보낸다. 상한(MaxItems)을 넘으면 가장 오래된 것부터 버려서 자동등록에 영향을 주지 않는다.
    /// </summary>
    public class LogQueue
    {
        public const int DefaultMaxItems = 5000;
        private readonly LinkedList<LogEntry> _items = new LinkedList<LogEntry>();
        private readonly object _gate = new object();
        private readonly string _file;
        public int MaxItems;
        public long Dropped { get; private set; }

        public LogQueue(string persistPath, int maxItems = DefaultMaxItems) { _file = persistPath; MaxItems = maxItems; }

        public int Count { get { lock (_gate) return _items.Count; } }

        public void Enqueue(LogEntry e)
        {
            lock (_gate)
            {
                _items.AddLast(e);
                while (_items.Count > MaxItems) { _items.RemoveFirst(); Dropped++; }
            }
        }

        /// <summary>앞에서부터 최대 n건을 꺼낸다(전송 실패 시 Requeue 로 되돌린다).</summary>
        public List<LogEntry> Take(int n)
        {
            var list = new List<LogEntry>();
            lock (_gate)
            {
                while (list.Count < n && _items.Count > 0) { list.Add(_items.First.Value); _items.RemoveFirst(); }
            }
            return list;
        }

        /// <summary>전송 실패한 묶음을 원래 순서대로 맨 앞에 되돌린다.</summary>
        public void Requeue(List<LogEntry> batch)
        {
            lock (_gate)
            {
                for (int i = batch.Count - 1; i >= 0; i--) _items.AddFirst(batch[i]);
                while (_items.Count > MaxItems) { _items.RemoveLast(); Dropped++; }
            }
        }

        /// <summary>디스크에 저장. 이때도 비밀정보 제거를 거친다.</summary>
        public void Persist()
        {
            if (string.IsNullOrEmpty(_file)) return;
            try
            {
                List<LogEntry> snap;
                lock (_gate) snap = new List<LogEntry>(_items);
                var sb = new StringBuilder();
                foreach (var e in snap)
                {
                    var c = JsonConvert.DeserializeObject<LogEntry>(JsonConvert.SerializeObject(e));
                    Redactor.ScrubEntry(c);
                    sb.Append(JsonConvert.SerializeObject(c)).Append('\n');
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_file));
                string tmp = _file + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(_file)) File.Delete(_file);
                File.Move(tmp, _file);
            }
            catch { }
        }

        /// <summary>이전 실행에서 못 보낸 것을 읽어 온다.</summary>
        public int Restore()
        {
            if (string.IsNullOrEmpty(_file) || !File.Exists(_file)) return 0;
            int n = 0;
            try
            {
                var restored = new List<LogEntry>();
                foreach (var line in File.ReadAllLines(_file, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { var e = JsonConvert.DeserializeObject<LogEntry>(line); if (e != null) { restored.Add(e); n++; } } catch { }
                }
                lock (_gate)
                {
                    for (int i = restored.Count - 1; i >= 0; i--) _items.AddFirst(restored[i]);
                    while (_items.Count > MaxItems) { _items.RemoveFirst(); Dropped++; }
                }
                File.Delete(_file);
            }
            catch { }
            return n;
        }
    }
}
