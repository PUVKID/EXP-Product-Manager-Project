using System;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Lumivolt.Security;

namespace BunjangAuto
{
    public static class TpmDevice
    {
        #if TESTBUILD
        public static readonly string KeyName = "EXP.DistributionTest.Device."+System.Diagnostics.Process.GetCurrentProcess().Id;
#else
        public const string KeyName = "Lumivolt.Device.v3";
#endif
        static readonly CngProvider Provider = new CngProvider("Microsoft Platform Crypto Provider");
        static readonly object Gate = new object();
        internal static CngKey Open(bool create)
        {
            lock (Gate)
            {
                try { var k = CngKey.Open(KeyName, Provider); Validate(k); return k; }
                catch (CryptographicException ex)
                {
                    uint code = unchecked((uint)ex.HResult);
                    if (!create || (code != 0x80090016 && code != 0x80090011)) throw;
                }
                var p = new CngKeyCreationParameters { Provider = Provider, ExportPolicy = CngExportPolicies.None, KeyUsage = CngKeyUsages.Signing };
                var key = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, p);
                Validate(key); return key;
            }
        }
        static void Validate(CngKey k)
        {
            // Some Platform KSP versions report generic ECDSA rather than ECDSA_P256.
            // Validate group AND the exact exported P-256 public blob, never the alias alone.
            if (k.Provider.Provider != Provider.Provider || k.AlgorithmGroup != CngAlgorithmGroup.ECDsa || k.ExportPolicy != CngExportPolicies.None || k.IsEphemeral)
            { k.Dispose(); throw new CryptographicException("TPM 비이동형 키 정책이 일치하지 않습니다."); }
            LicenseProtocol.PublicBlob(Convert.ToBase64String(k.Export(CngKeyBlobFormat.EccPublicBlob)));
        }
        public static string Error(Exception ex)
        {
            var c = ex as CryptographicException;
            uint code = c == null ? 0 : unchecked((uint)c.HResult);
            if (code == 0x80090010 || code == 0x80070005) return "TPM 키 접근 권한이 없습니다. 등록한 Windows 계정으로 실행해 주세요.";
            if (code == 0x80090016 || code == 0x80090011) return "기기 키를 찾을 수 없습니다. TPM 초기화/Windows 계정 변경 시 등록 요청을 새로 발급해 주세요.";
            if (code == 0x80090029 || code == 0x80090030 || code == 0x8028400F || ex is PlatformNotSupportedException) return "TPM 또는 P-256 키를 사용할 수 없습니다. Windows TPM 상태를 확인해 주세요.";
            return "TPM 키 생성/접근에 실패했습니다. Windows TPM 상태를 확인해 주세요. (0x" + code.ToString("X8") + ")";
        }
        public static string DisplayId()
        {
            try { using (var k = Open(false)) return LicenseProtocol.Fingerprint(k.Export(CngKeyBlobFormat.EccPublicBlob)).Substring(0, 16); }
            catch { return "등록 요청 내보내기 필요"; }
        }
        public static string CreateRequest()
        {
            using (var k = Open(true))
            using (var e = new ECDsaCng(k) { HashAlgorithm = CngAlgorithm.Sha256 })
            {
                var nonce = new byte[32]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(nonce);
                var o = new JObject { { "version", 3 }, { "product", LicenseProtocol.Product }, { "requestId", Guid.NewGuid().ToString("D") }, { "createdUtc", LicenseProtocol.Utc(DateTime.UtcNow) }, { "devicePublicKey", Convert.ToBase64String(k.Export(CngKeyBlobFormat.EccPublicBlob)) }, { "provider", Provider.Provider }, { "nonce", Convert.ToBase64String(nonce) } };
                var data = LicenseProtocol.Bytes(o);
                return LicenseProtocol.Wrap("LVR3", data, e.SignData(LicenseProtocol.RequestBytes(data)));
            }
        }
        public static void Prove(string expected)
        {
            using (var k = Open(false))
            using (var e = new ECDsaCng(k) { HashAlgorithm = CngAlgorithm.Sha256 })
            {
                var pub = k.Export(CngKeyBlobFormat.EccPublicBlob);
                if (Convert.ToBase64String(pub) != expected) throw new CryptographicException("이 PC용 라이선스가 아닙니다.");
                var challenge = new byte[32]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(challenge);
                if (!LicenseProtocol.DeviceProof(LicenseProtocol.PublicBlob(expected), challenge, e.SignData(challenge))) throw new CryptographicException("TPM 기기 소유 증명에 실패했습니다.");
            }
        }
    }
}
