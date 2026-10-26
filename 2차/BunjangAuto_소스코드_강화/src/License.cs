using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace BunjangAuto
{
    /// <summary>라이선스 키 검증. 키 = Base64(내용) + "." + Base64(RSA-3072 서명). 서명은 관리자 개인키로만 만들 수 있다.</summary>
    public class LicenseInfo
    {
        public bool Valid;
        public string Message = "";
        public string Name = "";
        public string MachineId = "";
        public DateTime Expires;      // DateTime.MaxValue = 무기한
        public DateTime Issued;
    }

    public static class License
    {
        // 공개키(XML)는 평문으로 두지 않고 변형해 둔다.
        private static readonly string _b =
            "ZpFEz2aUHc1iuzvHZP9a4UmECO5w6Sf4KJJh+3raVMJJ4AeNL/R+/WG4F+Fa" +
            "ggLwIIU4/V3DS95qmjnsKfVF/0PHB+tZpGXtapdOxkCGV80s5SrhEuxg+1Sf" +
            "As4xoCjLAIhQ9ErEMNNimCnXOfF97X6TPfpioSDGa7QlxhyTINxB5yHQY5Vy" +
            "xGWJDeFpnSKNHvp/+xilU9BqhwTHFKlmw1nBEvpguBzIM45/vWaoErAyoDbb" +
            "M6F63h6WNtN2gw/sN4lVuH7HAO16j3rHNJNi6nqJS95nkzb3avJvygKHM6lk" +
            "oAbHavtH1BWWVNpppSjbO7pT6HqlJqpEgiLVNrF6ym6mDsFsmzvSD/NlwV6m" +
            "LNZngBfmL5By5me9Xf0xmBqaOJtU/QaEIvJ5mBjGPLVu9n3BXNo0mi2bFpdS" +
            "zRjBUrRtkj/FF7BxzVu4F8Ng4A/RN7FZxUaGEqM7lQTadbFnoVSDFONpgC3J" +
            "DPVN5Xq3DP864wvkFKVu7RqkDt0yhCLtauwlx1SZVdFtnn7rcZdY1l+dCM1H" +
            "hQCRbptZ4R6eJuw2lQXTDawk20a3D9VzpHqRDPVC3F7AMMt0hw3qCPAg1Bmk" +
            "Uu5ZhHr1M5R1vhq0Bd8x/HuSa6cm31WUS/xmlijIMaFtwB6aJ+pIlgTnFoEv" +
            "/E62Btp6uy3oA4JY1l+UDu4xhHzEKKpEz1uaUa80shuNdYJv/267WLROuCrX" +
            "NrZksBG0HOtsuSvMLv1W32yzWLRGrz7NNKZ5+hPNS8lQlgXHI5V24liUWg==";

        private static string PublicKeyXml
        {
            get
            {
                byte[] k = new byte[] { 0x5A, 0xC3, 0x17, 0x8E, 0x2D, 0xF1, 0x64, 0x9B, 0x03, 0xD7, 0x4E, 0xA2 };
                byte[] d = Convert.FromBase64String(_b);
                for (int i = 0; i < d.Length; i++) d[i] = (byte)(d[i] ^ k[i % k.Length]);
                return Encoding.UTF8.GetString(d);
            }
        }

        /// <summary>이 PC 고유번호 (Windows MachineGuid 기반, 12자리)</summary>
        public static string GetMachineId()
        {
            string g = "";
            try
            {
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    if (k != null) g = Convert.ToString(k.GetValue("MachineGuid"));
            }
            catch { }
            if (string.IsNullOrEmpty(g)) g = Environment.MachineName + Environment.UserName;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes("BJA|" + g));
                string hex = BitConverter.ToString(h).Replace("-", "").Substring(0, 12);
                return hex.Substring(0, 4) + "-" + hex.Substring(4, 4) + "-" + hex.Substring(8, 4);
            }
        }

        public static string Payload(string name, string machineId, DateTime? expires)
        {
            return "BJA2|" + name.Replace("|", " ") + "|" + machineId.Trim().ToUpperInvariant() + "|" +
                   (expires.HasValue ? expires.Value.ToString("yyyyMMdd") : "0") + "|" + DateTime.Now.ToString("yyyyMMdd") +
                   "|" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static LicenseInfo Verify(string key, string myMachineId)
        {
            var r = new LicenseInfo();
            try
            {
                key = (key ?? "").Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
                int dot = key.IndexOf('.');
                if (dot <= 0) { r.Message = "라이선스 키 형식이 올바르지 않습니다."; return r; }
                byte[] payload = Convert.FromBase64String(key.Substring(0, dot));
                byte[] sig = Convert.FromBase64String(key.Substring(dot + 1));
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKeyXml);
                    if (!rsa.VerifyData(payload, "SHA256", sig)) { r.Message = "라이선스 키가 올바르지 않습니다."; return r; }
                }
                string[] p = Encoding.UTF8.GetString(payload).Split('|');
                if (p.Length != 6 || p[0] != "BJA2") { r.Message = "라이선스 키 형식이 올바르지 않습니다."; return r; }
                r.Name = p[1]; r.MachineId = p[2];
                r.Expires = p[3] == "0" ? DateTime.MaxValue : DateTime.ParseExact(p[3], "yyyyMMdd", null).AddDays(1).AddSeconds(-1);
                r.Issued = DateTime.ParseExact(p[4], "yyyyMMdd", null);
                if (!string.Equals(r.MachineId, myMachineId, StringComparison.OrdinalIgnoreCase))
                { r.Message = "이 PC용 라이선스가 아닙니다."; return r; }
                if (DateTime.Now < r.Issued.AddDays(-1)) { r.Message = "시스템 날짜가 올바르지 않습니다."; return r; }
                if (DateTime.Now > r.Expires) { r.Message = "라이선스 기간이 만료되었습니다. (" + r.Expires.ToString("yyyy-MM-dd") + ")"; return r; }
                r.Valid = true;
                r.Message = r.Expires == DateTime.MaxValue ? "무기한" : "만료일 " + r.Expires.ToString("yyyy-MM-dd");
                return r;
            }
            catch (Exception) { r.Message = "라이선스 키를 읽을 수 없습니다."; return r; }
        }

        public static string Sign(string privateXml, string payload)
        {
            byte[] data = Encoding.UTF8.GetBytes(payload);
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateXml);
                byte[] sig = rsa.SignData(data, "SHA256");
                return Convert.ToBase64String(data) + "." + Convert.ToBase64String(sig);
            }
        }
    }
}
