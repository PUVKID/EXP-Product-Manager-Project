using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    public class HttpResult
    {
        public int Status;          // 0 = 네트워크 오류
        public string Body = "";
        public string Error = "";
    }

    /// <summary>HTTP 전송 계층(테스트에서 가짜로 바꿔 끼울 수 있다).</summary>
    public interface ITransport
    {
        HttpResult Send(string method, string url, string jsonBody, Dictionary<string, string> headers, int timeoutMs);
    }

    public class HttpTransport : ITransport
    {
        static HttpTransport()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }   // TLS 1.2 이상
        }

        public HttpResult Send(string method, string url, string jsonBody, Dictionary<string, string> headers, int timeoutMs)
        {
            var r = new HttpResult();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = method;
                req.Timeout = timeoutMs; req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = "Lumivolt/" + ClientIdentityService.AppVersion;
                req.Proxy = WebRequest.DefaultWebProxy;
                if (headers != null) foreach (var kv in headers) req.Headers[kv.Key] = kv.Value;
                if (jsonBody != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(jsonBody);
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = data.Length;
                    using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    r.Status = (int)resp.StatusCode;
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) r.Body = sr.ReadToEnd();
                }
            }
            catch (WebException wx)
            {
                var resp = wx.Response as HttpWebResponse;
                if (resp != null)
                {
                    r.Status = (int)resp.StatusCode;
                    try { using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) r.Body = sr.ReadToEnd(); } catch { }
                }
                r.Error = wx.Status + ": " + wx.Message;
            }
            catch (Exception ex) { r.Error = ex.GetType().Name + ": " + ex.Message; }
            return r;
        }
    }

    /// <summary>
    /// 백그라운드 스레드에서 대기열을 묶음(batch)으로 HTTPS 로 보낸다.
    /// 어떤 실패도 예외로 밖에 나가지 않으며, 자동등록(Runner/Bot)과는 완전히 별도 스레드다.
    /// </summary>
    public class CentralLogService
    {
        private readonly TelemetryConfig _cfg;
        private readonly LogQueue _queue;
        private readonly ITransport _http;
        private Thread _thread;
        private volatile bool _stop;
        private readonly ManualResetEventSlim _wake = new ManualResetEventSlim(false);
        private string _token = "";
        private DateTime _lastInfo = DateTime.MinValue;
        private int _fail;                      // 연속 실패 횟수 → 지수 백오프
        private DateTime _lastNetLog = DateTime.MinValue;

        /// <summary>테스트에서 로컬 로그 파일을 어지럽히지 않기 위한 스위치</summary>
        public bool SuppressLocalLog;
        public long Sent { get; private set; }
        public string LastError { get; private set; }
        public bool Online { get; private set; }

        public CentralLogService(TelemetryConfig cfg, LogQueue queue, ITransport http)
        {
            _cfg = cfg; _queue = queue; _http = http ?? new HttpTransport();
            LastError = "";
        }

        private string TokenPath { get { return Path.Combine(Paths.Root, "telemetry.token"); } }

        private string Url(string path) { return _cfg.ServerUrl.TrimEnd('/') + path; }

        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "CentralLog" };
            _thread.Priority = ThreadPriority.BelowNormal;
            _thread.Start();
        }

        public void Poke() { _wake.Set(); }

        public void Stop(int waitMs)
        {
            _stop = true; _wake.Set();
            try { if (_thread != null) _thread.Join(waitMs); } catch { }
        }

        private void Loop()
        {
            LoadToken();
            while (!_stop)
            {
                try
                {
                    int waitMs = _cfg.FlushSeconds * 1000;
                    if (_fail > 0) waitMs = Math.Min(15 * 60 * 1000, waitMs * (1 << Math.Min(_fail, 6)));   // 10s, 20s, 40s ... 최대 15분
                    if (_queue.Count < _cfg.BatchSize) { _wake.Wait(waitMs); }
                    _wake.Reset();
                    if (_stop) break;
                    FlushOnce();
                }
                catch { }
            }
        }

        /// <summary>대기열 한 묶음을 보낸다. 성공하면 true. (테스트/종료 시에도 직접 호출)</summary>
        public bool FlushOnce()
        {
            if (_queue.Count == 0) return true;
            if (!EnsureRegistered()) return Fail(LastError.Length > 0 ? LastError : "등록 실패");

            var batch = _queue.Take(_cfg.BatchSize);
            if (batch.Count == 0) return true;
            var send = new List<LogEntry>(batch.Count);
            foreach (var e in batch)
            {
                var c = JsonConvert.DeserializeObject<LogEntry>(JsonConvert.SerializeObject(e));
                Redactor.ScrubEntry(c);               // 전송 직전 비밀정보 제거
                send.Add(c);
            }
            var body = new JObject();
            if ((DateTime.UtcNow - _lastInfo).TotalMinutes > 10 || _lastInfo == DateTime.MinValue)
                body["info"] = JObject.FromObject(ClientIdentityService.Describe());
            body["events"] = JArray.FromObject(send);

            var res = _http.Send("POST", Url("/api/v1/ingest"), body.ToString(Formatting.None), AuthHeaders(), 15000);
            if (res.Status == 200)
            {
                _lastInfo = DateTime.UtcNow; _fail = 0; Online = true; Sent += batch.Count; LastError = "";
                return true;
            }
            _queue.Requeue(batch);
            if (res.Status == 401) { _token = ""; TryDelete(TokenPath); }        // 토큰 재발급
            if (res.Status == 400) { _queue.Take(batch.Count); _fail = 0; return false; }   // 형식 오류 묶음은 버려 무한 재시도를 막는다
            return Fail(res.Status == 0 ? res.Error : "HTTP " + res.Status);
        }

        private bool Fail(string why)
        {
            _fail++; Online = false; LastError = why;
            // 네트워크 오류 이벤트는 10분에 한 번만 남긴다(대기열에 들어가 나중에 전송됨)
            if ((DateTime.UtcNow - _lastNetLog).TotalMinutes >= 10)
            {
                _lastNetLog = DateTime.UtcNow;
                if (!SuppressLocalLog) try { Log.Write("WARN", "Telemetry", "network.error", "중앙 로그 서버 전송 실패(자동으로 재시도합니다): " + why, "FAIL"); } catch { }
            }
            _queue.Persist();
            return false;
        }

        private Dictionary<string, string> AuthHeaders()
        {
            return new Dictionary<string, string> { { "X-Client-Id", ClientIdentityService.ClientId }, { "X-Client-Token", _token } };
        }

        private bool EnsureRegistered()
        {
            if (!string.IsNullOrEmpty(_token)) return true;
            var body = new JObject();
            body["client_id"] = ClientIdentityService.ClientId;
            body["info"] = JObject.FromObject(ClientIdentityService.Describe());
            var res = _http.Send("POST", Url("/api/v1/register"), body.ToString(Formatting.None),
                new Dictionary<string, string> { { "X-Ingest-Key", _cfg.IngestKey } }, 15000);
            if (res.Status != 200) { LastError = res.Status == 0 ? res.Error : "등록 HTTP " + res.Status; return false; }
            try
            {
                _token = (string)JObject.Parse(res.Body)["token"];
                if (string.IsNullOrEmpty(_token)) return false;
                try { File.WriteAllText(TokenPath, Vault.Protect(_token, "telemetry"), Encoding.UTF8); } catch { }
                _lastInfo = DateTime.UtcNow;
                return true;
            }
            catch { return false; }
        }

        private void LoadToken()
        {
            try
            {
                if (File.Exists(TokenPath)) _token = Vault.Unprotect(File.ReadAllText(TokenPath, Encoding.UTF8), "telemetry");
            }
            catch { _token = ""; }
        }

        private static void TryDelete(string p) { try { File.Delete(p); } catch { } }
    }
}
