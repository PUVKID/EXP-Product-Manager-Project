using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Lumivolt.Security
{
    // Sign the ORIGINAL UTF-8 payload bytes. Never deserialize/re-serialize before verification.
    public static class LicenseProtocol
    {
        public const string Product = "Lumivolt";
        public const int MaxText = 32768;
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Utc(DateTime t) { return t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture); }
        public static DateTime Time(string t) { return DateTime.ParseExact(t, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal); }
        public static byte[] Bytes(JObject o) { return Utf8.GetBytes(o.ToString(Formatting.None)); }
        public static string Fingerprint(byte[] b) { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", ""); }
        public static string KeyId(byte[] b) { if (b.Length != 32) throw new FormatException("Ed25519 공개키 길이 오류"); return Fingerprint(b).Substring(0, 32); }
        public static string ReadFile(string path)
        {
            if (new FileInfo(path).Length > MaxText) throw new FormatException("파일이 너무 큽니다.");
            return File.ReadAllText(path, Utf8);
        }
        public static JObject Parse(byte[] data)
        {
            if (data.Length > 16384) throw new FormatException("데이터가 너무 큽니다.");
            using (var sr = new StringReader(Utf8.GetString(data)))
            using (var r = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 })
            {
                var o = JObject.Load(r, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (r.Read()) throw new FormatException("추가 JSON 데이터가 있습니다.");
                return o;
            }
        }
        public static string Text(JObject o, string name, int max)
        {
            var v = o[name];
            if (v == null || v.Type != JTokenType.String || ((string)v).Length == 0 || ((string)v).Length > max) throw new FormatException("필드 오류: " + name);
            return (string)v;
        }
        static void Fields(JObject o, string fields)
        {
            var names = fields.Split(',');
            if (o.Count != names.Length || names.Any(n => o[n] == null)) throw new FormatException("지원하지 않는 필드 구성입니다.");
        }
        static void Version(JObject o)
        {
            if (o["version"].Type != JTokenType.Integer || (int)o["version"] != 3) throw new FormatException("지원하지 않는 라이선스 버전입니다.");
            if (Text(o, "product", 32) != Product) throw new FormatException("다른 제품의 라이선스입니다.");
        }
        public static byte[] PublicBlob(string text)
        {
            byte[] b = Convert.FromBase64String(text);
            // BCRYPT_ECDSA_PUBLIC_P256_MAGIC, cbKey=32, X || Y (little-endian header).
            if (b.Length != 72 || BitConverter.ToUInt32(b, 0) != 0x31534345 || BitConverter.ToUInt32(b, 4) != 32) throw new FormatException("P-256 기기 공개키 형식 오류");
            using (var k = CngKey.Import(b, CngKeyBlobFormat.EccPublicBlob)) { }
            return b;
        }
        public static byte[] RequestBytes(byte[] payload) { return Utf8.GetBytes("Lumivolt.Request.v3\n").Concat(payload).ToArray(); }
        public static void Decode(string token, string prefix, out byte[] payload, out byte[] sig)
        {
            if (token == null || token.Length > MaxText) throw new FormatException("라이선스 크기 오류");
            var p = token.Trim().Split('.');
            if (p.Length != 3 || p[0] != prefix) throw new FormatException("새 형식의 라이선스/등록 요청 파일이 필요합니다.");
            payload = Convert.FromBase64String(p[1]); sig = Convert.FromBase64String(p[2]);
            if (payload.Length == 0 || payload.Length > 16384 || sig.Length != 64) throw new FormatException("서명 형식 오류");
        }
        public static string Wrap(string prefix, byte[] payload, byte[] sig) { return prefix + "." + Convert.ToBase64String(payload) + "." + Convert.ToBase64String(sig); }
        public static bool DeviceProof(byte[] pub, byte[] data, byte[] sig)
        {
            using (var k = CngKey.Import(pub, CngKeyBlobFormat.EccPublicBlob))
            using (var e = new ECDsaCng(k) { HashAlgorithm = CngAlgorithm.Sha256 }) return e.VerifyData(data, sig);
        }
        public static JObject VerifyRequest(string token, DateTime now)
        {
            byte[] data, sig; Decode(token, "LVR3", out data, out sig);
            var o = Parse(data);
            Fields(o, "version,product,requestId,createdUtc,devicePublicKey,provider,nonce"); Version(o);
            Guid.ParseExact(Text(o, "requestId", 36), "D");
            if (Text(o, "provider", 64) != "Microsoft Platform Crypto Provider") throw new FormatException("TPM 등록 요청이 아닙니다.");
            var created = Time(Text(o, "createdUtc", 32));
            if (created > now.AddMinutes(5) || created < now.AddDays(-30)) throw new FormatException("등록 요청을 새로 내보내 주세요 (유효기간 30일).");
            if (Convert.FromBase64String(Text(o, "nonce", 64)).Length != 32) throw new FormatException("요청 난수 오류");
            var pub = PublicBlob(Text(o, "devicePublicKey", 128));
            if (!DeviceProof(pub, RequestBytes(data), sig)) throw new CryptographicException("등록 요청 서명이 올바르지 않습니다.");
            return o;
        }
        public static JObject VerifyLicense(string token, IDictionary<string, byte[]> trusted, DateTime now, string requiredFeature)
        {
            byte[] data, sig; Decode(token, "LV3", out data, out sig);
            var o = Parse(data);
            byte[] pub;
            if (!trusted.TryGetValue(Text(o, "keyId", 32), out pub)) throw new CryptographicException("등록되지 않은 발급키입니다.");
            var verifier = new Ed25519Signer(); verifier.Init(false, new Ed25519PublicKeyParameters(pub, 0));
            verifier.BlockUpdate(data, 0, data.Length);
            if (!verifier.VerifySignature(sig)) throw new CryptographicException("라이선스 서명이 올바르지 않습니다.");
            Fields(o, "version,licenseId,product,name,issuedUtc,expiresUtc,perpetual,features,devicePublicKey,keyId,status"); Version(o);
            Guid.ParseExact(Text(o, "licenseId", 36), "D"); Text(o, "name", 120);
            PublicBlob(Text(o, "devicePublicKey", 128));
            if (Text(o, "status", 16) != "active") throw new FormatException("활성 라이선스가 아닙니다.");
            var issued = Time(Text(o, "issuedUtc", 32));
            if (issued > now.AddMinutes(5)) throw new FormatException("발급 시각 이전입니다. PC 날짜를 확인해 주세요.");
            if (o["perpetual"].Type != JTokenType.Boolean) throw new FormatException("기간 형식 오류");
            if ((bool)o["perpetual"])
            { if (o["expiresUtc"].Type != JTokenType.Null) throw new FormatException("영구 라이선스 기간 오류"); }
            else
            {
                var expires = Time(Text(o, "expiresUtc", 32));
                if (expires <= issued || now >= expires) throw new FormatException("라이선스 기간이 만료되었거나 잘못되었습니다.");
            }
            var features = o["features"] as JArray;
            if (features == null || features.Count != 1 || features[0].Type != JTokenType.String || (string)features[0] != "automation" || requiredFeature != "automation") throw new FormatException("허용되지 않은 기능입니다.");
            return o;
        }
    }
}
