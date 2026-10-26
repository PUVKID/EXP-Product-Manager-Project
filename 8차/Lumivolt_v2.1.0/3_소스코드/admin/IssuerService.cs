using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Lumivolt.Security;

namespace Lumivolt.Admin
{
    public static class IssuerService
    {
        static readonly byte[] Entropy = LicenseProtocol.Utf8.GetBytes("Lumivolt.Admin.Ed25519.v3");
        public static string LastVault
        {
            get { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Lumivolt\Issuer")) return k == null ? "" : Convert.ToString(k.GetValue("Vault", "")); }
            set { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Lumivolt\Issuer")) k.SetValue("Vault", value); }
        }
        public static void SecureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(path, acl);
        }
        public static void Create(string directory)
        {
            directory = Path.GetFullPath(directory);
            // Never silently replace an issuer: previously issued licenses must remain verifiable.
            if (File.Exists(Path.Combine(directory, "issuer.dpapi"))) throw new IOException("이 폴더에는 이미 발급키가 있습니다.");
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0) throw new IOException("비어 있는 새 관리자 폴더를 선택해 주세요.");
            SecureDirectory(directory);
            var privateKey = new Ed25519PrivateKeyParameters(new SecureRandom());
            byte[] seed = privateKey.GetEncoded();
            try
            {
                File.WriteAllBytes(Path.Combine(directory, "issuer.dpapi"), ProtectedData.Protect(seed, Entropy, DataProtectionScope.CurrentUser));
                ExportPublic(directory, Path.Combine(directory, "issuer-public.json"));
                LastVault = directory;
            }
            finally { Array.Clear(seed, 0, seed.Length); }
        }
        public static Ed25519PrivateKeyParameters Load(string directory)
        {
            var path = Path.Combine(directory, "issuer.dpapi");
            if (new FileInfo(path).Length > 8192) throw new FormatException("발급키 파일 오류");
            byte[] seed = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            try { if (seed.Length != 32) throw new FormatException("발급키 길이 오류"); return new Ed25519PrivateKeyParameters(seed, 0); }
            finally { Array.Clear(seed, 0, seed.Length); }
        }
        public static JObject Public(string directory)
        {
            var pub = Load(directory).GeneratePublicKey().GetEncoded();
            return new JObject { { "keyId", LicenseProtocol.KeyId(pub) }, { "publicKey", Convert.ToBase64String(pub) } };
        }
        public static void ExportPublic(string directory, string file) { File.WriteAllText(file, Public(directory).ToString(), LicenseProtocol.Utf8); }
        public static string Issue(string directory, string request, string name, DateTime? expires)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new FormatException("이름/메모를 1~120자로 입력해 주세요.");
            if (expires.HasValue && expires.Value.ToUniversalTime() <= DateTime.UtcNow) throw new FormatException("미래의 만료일을 선택해 주세요.");
            var req = LicenseProtocol.VerifyRequest(request, DateTime.UtcNow);
            var key = Load(directory); var pub = key.GeneratePublicKey().GetEncoded();
            var o = new JObject { { "version", 3 }, { "licenseId", Guid.NewGuid().ToString("D") }, { "product", LicenseProtocol.Product }, { "name", name.Trim() }, { "issuedUtc", LicenseProtocol.Utc(DateTime.UtcNow) }, { "expiresUtc", expires.HasValue ? (JToken)LicenseProtocol.Utc(expires.Value) : JValue.CreateNull() }, { "perpetual", !expires.HasValue }, { "features", new JArray("automation") }, { "devicePublicKey", req["devicePublicKey"].DeepClone() }, { "keyId", LicenseProtocol.KeyId(pub) }, { "status", "active" } };
            byte[] data = LicenseProtocol.Bytes(o);
            var signer = new Ed25519Signer(); signer.Init(true, key); signer.BlockUpdate(data, 0, data.Length);
            string token = LicenseProtocol.Wrap("LV3", data, signer.GenerateSignature());
            // Verify before storing. Issuance records contain public license data only.
            LicenseProtocol.VerifyLicense(token, new Dictionary<string, byte[]> { { LicenseProtocol.KeyId(pub), pub } }, DateTime.UtcNow, "automation");
            string records = Path.Combine(directory, "records"); Directory.CreateDirectory(records);
            File.WriteAllText(Path.Combine(records, (string)o["licenseId"] + ".lvlicense"), token, LicenseProtocol.Utf8);
            return token;
        }
        public static JObject Inspect(string directory, string token)
        {
            var pub = Load(directory).GeneratePublicKey().GetEncoded();
            // Inspect historical expired records at their issuance time, preserving signature verification.
            byte[] data, sig; LicenseProtocol.Decode(token, "LV3", out data, out sig);
            var o = LicenseProtocol.Parse(data);
            return LicenseProtocol.VerifyLicense(token, new Dictionary<string, byte[]> { { LicenseProtocol.KeyId(pub), pub } }, LicenseProtocol.Time(LicenseProtocol.Text(o, "issuedUtc", 32)), "automation");
        }
        public static void ExportRecords(string directory, string target)
        {
            var rows = new JArray(); var records = Path.Combine(directory, "records");
            if (Directory.Exists(records)) foreach (string f in Directory.GetFiles(records, "*.lvlicense")) rows.Add(Inspect(directory, LicenseProtocol.ReadFile(f)));
            File.WriteAllText(target, rows.ToString(), LicenseProtocol.Utf8);
        }
        // Portable backups use library AES-256-GCM + framework PBKDF2-HMAC-SHA256.
        // DPAPI remains the normal at-rest format. Never persist the plaintext seed.
        static byte[] BackupCrypt(bool encrypt, byte[] input, string password, byte[] salt, byte[] nonce)
        {
            if (password == null || password.Length < 12) throw new FormatException("백업 암호는 12자 이상으로 정해 주세요.");
            byte[] derived;
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256)) derived = kdf.GetBytes(32);
            try
            {
                var cipher = new Org.BouncyCastle.Crypto.Modes.GcmBlockCipher(new Org.BouncyCastle.Crypto.Engines.AesEngine());
                cipher.Init(encrypt, new Org.BouncyCastle.Crypto.Parameters.AeadParameters(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(derived), 128, nonce, LicenseProtocol.Utf8.GetBytes("Lumivolt.AdminBackup.v1")));
                var output = new byte[cipher.GetOutputSize(input.Length)];
                int count = cipher.ProcessBytes(input, 0, input.Length, output, 0); count += cipher.DoFinal(output, count);
                Array.Resize(ref output, count); return output;
            }
            finally { Array.Clear(derived, 0, derived.Length); }
        }
        public static void Backup(string directory, string target, string password)
        {
            var salt = new byte[32]; var nonce = new byte[12]; using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(nonce); }
            byte[] seed = Load(directory).GetEncoded();
            try
            {
                var cipher = BackupCrypt(true, seed, password, salt, nonce);
                var o = new JObject { { "version", 1 }, { "salt", Convert.ToBase64String(salt) }, { "nonce", Convert.ToBase64String(nonce) }, { "ciphertext", Convert.ToBase64String(cipher) } };
                using (var f = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) { var b = LicenseProtocol.Bytes(o); f.Write(b, 0, b.Length); }
            }
            finally { Array.Clear(seed, 0, seed.Length); }
        }
        public static void Restore(string backup, string directory, string password)
        {
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0) throw new IOException("비어 있는 새 관리자 폴더를 선택해 주세요.");
            var o = LicenseProtocol.Parse(LicenseProtocol.Utf8.GetBytes(LicenseProtocol.ReadFile(backup)));
            if (o.Count != 4 || o["version"] == null || o["version"].Type != JTokenType.Integer || (int)o["version"] != 1) throw new FormatException("백업 형식 오류");
            var salt = Convert.FromBase64String(LicenseProtocol.Text(o, "salt", 64)); var nonce = Convert.FromBase64String(LicenseProtocol.Text(o, "nonce", 32)); var cipher = Convert.FromBase64String(LicenseProtocol.Text(o, "ciphertext", 128));
            if (salt.Length != 32 || nonce.Length != 12 || cipher.Length != 48) throw new FormatException("백업 형식 오류");
            byte[] seed = BackupCrypt(false, cipher, password, salt, nonce);
            try
            {
                if (seed.Length != 32) throw new FormatException("백업 키 오류");
                SecureDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "issuer.dpapi"), ProtectedData.Protect(seed, Entropy, DataProtectionScope.CurrentUser));
                ExportPublic(directory, Path.Combine(directory, "issuer-public.json")); LastVault = directory;
            }
            finally { Array.Clear(seed, 0, seed.Length); }
        }
    }
}
