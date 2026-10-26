using System;
using System.IO;
using System.Collections.Generic;
using BunjangAuto;
using Lumivolt.Security;
namespace Lumivolt.Admin
{
    public static class AdminWorkflow
    {
        public static bool IsMatchingVault(string folder)
        {
            try
            {
                var pub = IssuerService.Public(folder); byte[] expected;
                return TrustedKeys.All.TryGetValue((string)pub["keyId"], out expected) && Convert.ToBase64String(expected) == (string)pub["publicKey"];
            }
            catch { return false; }
        }
        public static string FindVault()
        {
            var folders = new List<string>();
            try { folders.Add(IssuerService.LastVault); } catch { }
            folders.Add(AppDomain.CurrentDomain.BaseDirectory);
            var parent = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (parent != null) folders.Add(parent.FullName);
            // Existing administrator vaults only. Never generate or switch to a new signing identity silently.
            try { folders.AddRange(Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lumivolt_AdminVault_*", SearchOption.TopDirectoryOnly)); } catch { }
            try { folders.AddRange(Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EXP_MasterVault_*", SearchOption.TopDirectoryOnly)); } catch { }
            foreach (var folder in folders) if (!String.IsNullOrWhiteSpace(folder) && IsMatchingVault(folder)) return folder;
            return "";
        }
        public static string Issue(string folder, string request, string name, DateTime? expires, string edition)
        {
            if (!IsMatchingVault(folder)) throw new InvalidOperationException("이 앱에 맞는 발급키를 찾지 못했습니다. 관리 메뉴에서 기존 키를 연결해 주세요.");
            request = LicenseText.Normalize(request, "LVR3");
            var req = LicenseProtocol.VerifyRequest(request, DateTime.UtcNow);
            if (String.IsNullOrWhiteSpace(name)) name = "고객-" + LicenseProtocol.Fingerprint(Convert.FromBase64String((string)req["devicePublicKey"])).Substring(0, 8);
            var token = IssuerService.Issue(folder, request, name, expires, edition);
            LicenseProtocol.VerifyLicense(token, TrustedKeys.All, DateTime.UtcNow, "automation");
            return token;
        }
    }
}
