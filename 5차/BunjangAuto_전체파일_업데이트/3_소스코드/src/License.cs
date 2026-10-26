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
        // 공개키(XML)는 평문으로 두지 않고 AES-256(CBC)으로 암호화해 둔다(여러 조각 상수를 조합해 키/IV를 만든다).
        private static readonly byte[] _k1 = { 0x4E,0x91,0xD2,0x3F,0x7A,0x0C,0x88,0x5B,0x1E,0x6D,0xA3,0x29,0xF4,0x57,0x9B,0xC0 };
        private static readonly byte[] _k2 = { 0x12,0xE8,0x6A,0xB5,0x3D,0xF0,0x74,0x9C,0x28,0x61,0xDA,0x4F,0x87,0x15,0xC9,0x3B };
        private static readonly byte[] _k3 = { 0xA7,0x5C,0x1F,0x92,0xE3,0x68,0xB0,0x4D,0x97,0x2A,0x6E,0xD1,0x53,0xF8,0x0A,0xC4 };
        private static readonly byte[] _iv1 = { 0x8B,0x3E,0xC7,0x14,0x5A,0x92,0xF6,0x2D,0x71,0xB8,0x4C,0xE0,0x39,0xA6,0x1D,0x85 };
        private static readonly byte[] _iv2 = { 0x27,0xD4,0x6F,0xB1,0x9A,0x3C,0x58,0xE2,0x0D,0x74,0xC1,0x4B,0x96,0x2E,0xF7,0x80 };

        // AES-256-CBC 로 암호화된 공개키 XML (Base64, RSA-4096)
        private static readonly string _b =
            "WWY21hNpelVvr2DRvp80OksHjFkWxTUk2vdxGdMV2jXdEs3+l1ZBLCwYUBMqRagW" +
            "L7tlScrbNkjId6D4uR4rjSunqtO3IuuC1Rtyt7QqktlMNvb8EUnNJ+oJRQaOilAV" +
            "e3JBGoem/qlO40pwprxR6dQfvP37eQJeSqM5+UebBvGENgVylhOWeRAek/vSVkj0" +
            "rSByZ1etai4cjdyqblUdtpw/HcQ7tmlX8W9rcHPqjtnuIXGNXjjM2GC0zp+iaLDQ" +
            "oJRKk/jl+mubnQ7MRycedNnCN2+C2+B0a2HsRoXia5rhDW9JrGxojowXQS083Eye" +
            "fJvBWU1a/1ombVeZYNKsLN04GsvNvsT3qbVXTC7+zgKq3azWQCXPCyuhtYW7fSfD" +
            "2Q7bmYHkPaWsvFn6kXq9t5U0rfjY/1rcQbaFBFjbXC68J/IWaJNagwGbt04wtzB7" +
            "BHY619eaCAKW5imRdFWfuy2tkBwYKzO89cheyHbmzQEPyxlQj3AJEAOA8p5oKoyB" +
            "0IPTHMcZH/NL+K7wGR9pd6iGjht7SXNDtIQIyjLg3P/MjtMBzgU0tP0cZi05IyqQ" +
            "6bxzF9EOgmwolGslsIYNM+d67IKk4ULD+3X3u4m+iFSm9p5vuCBZkUPj+o2qVu70" +
            "IRvDAI7+KmVOzAnY8h1CREjP8ZxfyAj+0tIj5TT4HOFrDx5aqT5whKsg9Tr1taB2" +
            "StfJT8tXFD4YM0Uf1tFKANSE/cfw0NfZfd+uYQ3CahjCHwtWOWrfRpzQXAE4Q66e" +
            "t1vu+yHcl3/bbY37314H77PhU4Dr+gwZlkLqsNC71zw684IVCTnINKVSO7Fd65+L" +
            "mIFkV7s5d0CNAdzx3xzmodxONbGO5sA3/1QqA4pHPnFSj7HD82nEp9IcxfhwCYyN" +
            "Rb6nFg00FbNNs7ybypHYD11GRIGmd4NwFgzGrEujaWkW+MWJIIArGAILFfumUq3p" +
            "eXCm5FGxrPBMNPiH16X/p/bGimrsEYdQZamVSM1cICaTX3ncTdlS2DuWpURDWmGA";

        private static byte[] Xor3(byte[] x, byte[] y, byte[] z)
        {
            var r = new byte[x.Length];
            for (int i = 0; i < x.Length; i++) r[i] = (byte)(x[i] ^ y[i] ^ z[i]);
            return r;
        }

        private static string PublicKeyXml
        {
            get
            {
                byte[] key = SHA256.Create().ComputeHash(Xor3(_k1, _k2, _k3));
                byte[] iv = Xor3(_iv1, _iv2, _iv1);
                Array.Resize(ref iv, 16);
                byte[] ct = Convert.FromBase64String(_b);
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                    aes.Key = key; aes.IV = iv;
                    using (var dec = aes.CreateDecryptor())
                        return Encoding.UTF8.GetString(dec.TransformFinalBlock(ct, 0, ct.Length));
                }
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
