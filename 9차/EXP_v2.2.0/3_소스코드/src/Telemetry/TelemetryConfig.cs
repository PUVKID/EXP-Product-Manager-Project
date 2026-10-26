using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>빌드할 때 build.bat/build.sh 가 BJA_SERVER_URL, BJA_INGEST_KEY 환경변수로 덮어써 생성하는 기본값. (Telemetry/BuildConfig.cs)</summary>
    public class TelemetryConfig
    {
        /// <summary>false 이면 중앙 서버로 아무것도 보내지 않는다(로컬 로그는 그대로 기록).</summary>
        public bool Enabled = true;
        /// <summary>중앙 로그 서버 주소. https:// 만 허용(개발용 localhost 는 http 허용). 비어 있으면 전송하지 않는다.</summary>
        public string ServerUrl = BuildConfig.ServerUrl;
        /// <summary>클라이언트 등록용 공개 키(EXE 안에 들어가므로 비밀이 아니다. 관리자 자격과 무관).</summary>
        public string IngestKey = BuildConfig.IngestKey;
        public int BatchSize = 50;
        public int FlushSeconds = 10;
        public int HeartbeatSeconds = 60;

        public static string FilePath { get { return Path.Combine(Paths.Root, "telemetry.json"); } }

        public static TelemetryConfig Load()
        {
            var c = new TelemetryConfig();
            try
            {
                if (File.Exists(FilePath))
                {
                    var j = JsonConvert.DeserializeObject<TelemetryConfig>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (j != null) c = j;
                }
                else
                {
                    Directory.CreateDirectory(Paths.Root);
                    File.WriteAllText(FilePath, JsonConvert.SerializeObject(c, Formatting.Indented), Encoding.UTF8);
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(c.ServerUrl)) c.ServerUrl = BuildConfig.ServerUrl;
            if (string.IsNullOrWhiteSpace(c.IngestKey)) c.IngestKey = BuildConfig.IngestKey;
            if (c.BatchSize < 1 || c.BatchSize > 500) c.BatchSize = 50;
            if (c.FlushSeconds < 1) c.FlushSeconds = 10;
            if (c.HeartbeatSeconds < 15) c.HeartbeatSeconds = 60;
            return c;
        }

        /// <summary>https 이거나, 개발용 루프백 주소일 때만 true.</summary>
        public bool UrlAllowed
        {
            get
            {
                Uri u;
                if (!Uri.TryCreate((ServerUrl ?? "").Trim(), UriKind.Absolute, out u)) return false;
                if (u.Scheme == Uri.UriSchemeHttps) return true;
                return u.Scheme == Uri.UriSchemeHttp && (u.IsLoopback);
            }
        }

        public bool Active { get { return Enabled && UrlAllowed && !string.IsNullOrWhiteSpace(IngestKey); } }
    }
}
