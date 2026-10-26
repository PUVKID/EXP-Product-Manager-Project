using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Lumivolt.Security;
namespace BunjangAuto {
public class LicenseInfo {
    public bool Valid; public string Message = "", Name = "", MachineId = "", LicenseId = "";
    public DateTime Expires, Issued;
}
public static class License {
// Compatibility ONLY: settings encryption and telemetry identity. Never an authorization credential.
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


    public static LicenseInfo Verify(string token, string diagnosticId) {
        var r = new LicenseInfo();
        try {
            var o = LicenseProtocol.VerifyLicense(token, TrustedKeys.All, DateTime.UtcNow, "automation");
            try { TpmDevice.Prove((string)o["devicePublicKey"]); }
            catch (CryptographicException ex) {
                if (ex.Message == "이 PC용 라이선스가 아닙니다." || ex.Message == "TPM 기기 소유 증명에 실패했습니다.") throw;
                throw new CryptographicException(TpmDevice.Error(ex));
            }
            r.Name = (string)o["name"]; r.MachineId = LicenseProtocol.Fingerprint(LicenseProtocol.PublicBlob((string)o["devicePublicKey"]));
            r.LicenseId = (string)o["licenseId"]; r.Issued = LicenseProtocol.Time((string)o["issuedUtc"]);
            r.Expires = (bool)o["perpetual"] ? DateTime.MaxValue : LicenseProtocol.Time((string)o["expiresUtc"]).ToLocalTime().AddSeconds(-1);
            r.Valid = true; r.Message = r.Expires == DateTime.MaxValue ? "무기한" : "만료일 " + r.Expires.ToString("yyyy-MM-dd");
        } catch (FormatException ex) { r.Message = ex.Message; }
          catch (CryptographicException ex) { r.Message = ex.Message; }
          catch { r.Message = "라이선스 파일이 손상되었거나 검증할 수 없습니다."; }
        return r;
    }
}}
