using System;
using System.IO;
using System.Security.Cryptography;
using Lumivolt.Security;
namespace BunjangAuto
{
    internal static class LicenseClock
    {
        static readonly object Gate = new object();
        static readonly byte[] Purpose = LicenseProtocol.Utf8.GetBytes("Lumivolt.Clock.v3");
        static string FilePath { get { return Path.Combine(Paths.Root, "license.clock.v3"); } }
        static DateTime Read()
        {
            if (!File.Exists(FilePath)) return DateTime.MinValue;
            if (new FileInfo(FilePath).Length > 8192) throw new FormatException("인증 시각 기록 오류");
            var clear = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Purpose, DataProtectionScope.CurrentUser);
            return LicenseProtocol.Time(LicenseProtocol.Utf8.GetString(clear));
        }
        public static bool Accept(bool update)
        {
            lock (Gate)
            {
                string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var seen = Read(); var now = DateTime.UtcNow;
                    if (now.AddMinutes(5) < seen) return false;
                    if (update)
                    {
                        Directory.CreateDirectory(Paths.Root);
                        var value = LicenseProtocol.Utc(now > seen ? now : seen);
                        File.WriteAllBytes(temp, ProtectedData.Protect(LicenseProtocol.Utf8.GetBytes(value), Purpose, DataProtectionScope.CurrentUser));
                        if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
                    }
                    return true;
                }
                catch { return false; }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }
        }
    }
}
