const fs = require('fs'), path = require('path');
const root = path.resolve('work/Lumivolt_v2.1.0/3_소스코드');
function edit(p, fn) { const f=path.join(root,p); fs.writeFileSync(f, '\ufeff'+fn(fs.readFileSync(f,'utf8').replace(/^\ufeff/,'')), 'utf8'); }
const old = fs.readFileSync(path.resolve('work/original/Lumivolt_v2.0.0/3_소스코드/src/License.cs'),'utf8');
const legacy = old.slice(old.indexOf('        public static string GetMachineId()'),old.indexOf('        public static string Payload('));
fs.writeFileSync(path.join(root,'src/License.cs'),`using System;
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
${legacy}
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
`);
edit('src/Program.cs',s=>s.replace('            if (args.Length > 0 && args[0] == "--selftest")','#if TESTBUILD\n            if (args.Length > 0 && args[0] == "--selftest")').replace('#if UIPREVIEW','#endif\n#if UIPREVIEW'));
for(const f of ['SelfTest.cs','SelfTestV2.cs']) edit('src/'+f,s=>'#if TESTBUILD\n'+s+'\n#endif\n');
edit('src/LicenseForm.cs',s=>{
 const a=s.indexOf('        public static bool Check()'), b=s.indexOf('        /// <summary>License 화면');
 s=s.slice(0,a)+`        public static bool Check()
        {
            string msg = "";
            if (Recheck()) return true;
            if (File.Exists(KeyPath)) msg = "기존 라이선스를 확인할 수 없습니다. 새 TPM 라이선스를 등록해 주세요.";
            using (var f = new LicenseForm(TpmDevice.DisplayId(), msg))
            {
                if (f.ShowDialog() != DialogResult.OK) return false;
                string result;
                if (!TryActivate(f.Key, out result)) { FluentBox.Show(null, result, "라이선스"); return false; }
                return true;
            }
        }

`+s.slice(b);
 s=s.replace('if (!info.Valid) { Log.Write','if (info.Valid && ClockRolledBack()) { message = "시스템 날짜 또는 인증 기록을 확인해 주세요."; return false; }\n            if (!info.Valid) { LastInfo = null; Log.Write');
 s=s.replace('File.ReadAllText(KeyPath, Encoding.UTF8)','Lumivolt.Security.LicenseProtocol.ReadFile(KeyPath)');
 s=s.replace('                if (!File.Exists(KeyPath)) return false;','                LastInfo = null;\n                if (!File.Exists(KeyPath)) return false;');
 s=s.replace('            catch { return false; }\n        }\n\n        private static void Touch','            catch { return true; }\n        }\n\n        private static void Touch');
 s=s.replace('이 PC의 고유번호 (관리자에게 전달해 주세요)','TPM 기기 번호 (등록 요청 파일을 관리자에게 전달해 주세요)');
 s=s.replace('            Controls.AddRange(new Control[] { lblCopy,','            var bRequest = new Button { Left = 20, Top = 330, Width = 136, Height = 32, Text = "등록 요청 저장" };\n            var bImport = new Button { Left = 162, Top = 330, Width = 142, Height = 32, Text = "라이선스 파일 열기" };\n            Controls.AddRange(new Control[] { bRequest, bImport, lblCopy,');
 s=s.replace('            bCopy.Click +=', '            bRequest.Click += delegate { LicenseFiles.ExportRequest(this); txtId.Text = TpmDevice.DisplayId(); };\n            bImport.Click += delegate { string token = LicenseFiles.ImportLicense(this); if (token != null) txtKey.Text = token; };\n            bCopy.Click +=');
 return s;
});
edit('src/UI/Pages/LicensePage.cs',s=>s.replace('            c1.Controls.AddRange(new Control[] { h1, chip, bCopy });','            var bRequest = Ui.Button("등록 요청 저장", "", BtnStyle.Standard, 150); bRequest.Location = new Point(430, 177);\n            bRequest.Click += delegate { LicenseFiles.ExportRequest(this); Sync(); };\n            c1.Controls.AddRange(new Control[] { h1, chip, bCopy, bRequest });').replace('            c2.Controls.AddRange(new Control[] { h2, d2, txtKey, bOk, lblMsg });','            var bImport = Ui.Button("파일 열기", "", BtnStyle.Standard, 110); bImport.Location = new Point(146, 152);\n            lblMsg.Location = new Point(272, 160);\n            bImport.Click += delegate { string token = LicenseFiles.ImportLicense(this); if (token != null) txtKey.Text = token; };\n            c2.Controls.AddRange(new Control[] { h2, d2, txtKey, bOk, bImport, lblMsg });').replace(/License.GetMachineId\(\)/g,'TpmDevice.DisplayId()'));
edit('src/Runner.cs',s=>s.replace('                        p.Status = "진행중";','                        Guard.Check();\n                        p.Status = "진행중";'));
edit('src/AssemblyInfo.cs',s=>s.replace(/2\.0\.0\.0/g,'2.1.0.0'));
