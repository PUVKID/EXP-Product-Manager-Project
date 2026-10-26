using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    /// <summary>
    /// EXE 안의 '관리자 센터'가 중앙 서버의 관리자 API 를 부르는 클라이언트.
    /// 관리자 아이디/비밀번호는 서버에서만 검증하며 EXE 에는 어떤 관리자 정보도 들어 있지 않다.
    /// 로그인에 성공하면 서버가 주는 세션 토큰(메모리에만 보관)으로 ADMIN 역할을 확인한다.
    /// </summary>
    public class AdminApiClient
    {
        private readonly ITransport _http;
        private readonly string _base;
        private string _token = "";
        public string Role = "USER";
        public string LastError = "";

        public AdminApiClient(string serverUrl, ITransport http = null)
        {
            _base = (serverUrl ?? "").TrimEnd('/');
            _http = http ?? new HttpTransport();
        }

        public bool IsAdmin { get { return Role == "ADMIN" && _token.Length > 0; } }

        private Dictionary<string, string> Auth()
        {
            var h = new Dictionary<string, string>();
            if (_token.Length > 0) h["Authorization"] = "Bearer " + _token;
            return h;
        }

        public bool Login(string user, string password)
        {
            var body = new JObject { { "username", user }, { "password", password } };
            var r = _http.Send("POST", _base + "/api/v1/admin/login", body.ToString(Formatting.None), null, 15000);
            if (r.Status != 200)
            {
                LastError = r.Status == 0 ? "서버에 연결할 수 없습니다. (" + r.Error + ")" : Msg(r);
                return false;
            }
            var j = JObject.Parse(r.Body);
            _token = (string)j["token"] ?? "";
            Role = (string)j["role"] ?? "USER";
            return IsAdmin;
        }

        public void Logout()
        {
            if (_token.Length > 0) _http.Send("POST", _base + "/api/v1/admin/logout", "{}", Auth(), 5000);
            _token = ""; Role = "USER";
        }

        private static string Msg(HttpResult r)
        {
            try { return (string)JObject.Parse(r.Body)["error"] ?? ("HTTP " + r.Status); } catch { return "HTTP " + r.Status; }
        }

        public JObject Get(string path)
        {
            var r = _http.Send("GET", _base + "/api/v1/admin/" + path, null, Auth(), 20000);
            if (r.Status == 200) { LastError = ""; return JObject.Parse(r.Body); }
            LastError = r.Status == 0 ? "서버에 연결할 수 없습니다." : Msg(r);
            if (r.Status == 401) { _token = ""; Role = "USER"; }
            return null;
        }

        private static string Q(IDictionary<string, string> q)
        {
            var sb = new StringBuilder();
            if (q != null)
                foreach (var kv in q)
                    if (!string.IsNullOrEmpty(kv.Value)) sb.Append(sb.Length == 0 ? "" : "&").Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
            return sb.ToString();
        }

        private static int TzMinutes() { return (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes; }

        public JObject Overview() { return Get("overview?tz=" + TzMinutes()); }
        public JObject Stats(int days) { return Get("stats?days=" + days + "&tz=" + TzMinutes()); }
        public JObject Users(IDictionary<string, string> q) { return Get("users?" + Q(q)); }
        public JObject UserDetail(string clientId) { return Get("users/" + Uri.EscapeDataString(clientId) + "?tz=" + TzMinutes()); }
        public JObject Logs(IDictionary<string, string> q) { return Get("logs?" + Q(q)); }
        public JObject Errors(int limit) { return Get("errors?limit=" + limit); }

        /// <summary>필터가 적용된 로그를 CSV 또는 JSON 텍스트로 받는다.</summary>
        public string Export(IDictionary<string, string> q, string format)
        {
            var d = new Dictionary<string, string>(q ?? new Dictionary<string, string>());
            d["format"] = format; d["download"] = "1"; d["limit"] = "50000";
            var r = _http.Send("GET", _base + "/api/v1/admin/logs?" + Q(d), null, Auth(), 60000);
            if (r.Status == 200) return r.Body;
            LastError = r.Status == 0 ? "서버에 연결할 수 없습니다." : Msg(r);
            return null;
        }
    }
}
