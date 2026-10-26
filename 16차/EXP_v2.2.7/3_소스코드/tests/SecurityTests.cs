using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Lumivolt.Security;
using Lumivolt.Admin;
using BunjangAuto;

class SecurityTests
{
    static int failures, passed;
    static Ed25519PrivateKeyParameters issuer;
    static Dictionary<string, byte[]> trusted;
    static DateTime now = DateTime.UtcNow;
    static void Check(string name, bool value) { Console.WriteLine((value ? "PASS " : "FAIL ") + name); if (value) passed++; else failures++; }
    static void Reject(string name, Action a) { try { a(); Check(name, false); } catch { Check(name, true); } }
    static string Sign(byte[] data) { var s = new Ed25519Signer(); s.Init(true, issuer); s.BlockUpdate(data, 0, data.Length); return LicenseProtocol.Wrap("LV3", data, s.GenerateSignature()); }
    static string Sign(JObject o) { return Sign(LicenseProtocol.Bytes(o)); }
    static JObject Verify(string t) { return LicenseProtocol.VerifyLicense(t, trusted, now, "automation"); }
    static string Request(CngKey key)
    {
        var random = new byte[32]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(random);
        var o = new JObject { { "version", 3 }, { "product", "Lumivolt" }, { "requestId", Guid.NewGuid().ToString("D") }, { "createdUtc", LicenseProtocol.Utc(now) }, { "devicePublicKey", Convert.ToBase64String(key.Export(CngKeyBlobFormat.EccPublicBlob)) }, { "provider", "Microsoft Platform Crypto Provider" }, { "nonce", Convert.ToBase64String(random) } };
        var data = LicenseProtocol.Bytes(o);
        using (var e = new ECDsaCng(key) { HashAlgorithm = CngAlgorithm.Sha256 }) return LicenseProtocol.Wrap("LVR3", data, e.SignData(LicenseProtocol.RequestBytes(data)));
    }
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } }
        };
        try { return Run(args); } catch (Exception ex) { Console.WriteLine("FAIL UNHANDLED " + ex); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
        if (args.Length == 2 && args[0] == "--tpm-request") { File.WriteAllText(args[1], TpmDevice.CreateRequest(), LicenseProtocol.Utf8); Console.WriteLine("TPM REQUEST CREATED"); return 0; }
        if (args.Length == 2 && args[0] == "--software-request") { using (var k = CngKey.Create(CngAlgorithm.ECDsaP256)) File.WriteAllText(args[1], Request(k), LicenseProtocol.Utf8); Console.WriteLine("SOFTWARE NEGATIVE-TEST REQUEST CREATED (NOT TPM ATTESTED)"); return 0; }
        if (args.Length == 2 && args[0] == "--tpm-prove") { var req = LicenseProtocol.VerifyRequest(LicenseProtocol.ReadFile(args[1]), DateTime.UtcNow); TpmDevice.Prove((string)req["devicePublicKey"]); Console.WriteLine("TPM REOPEN + PROOF PASSED"); return 0; }
        issuer = new Ed25519PrivateKeyParameters(new SecureRandom()); var pub = issuer.GeneratePublicKey().GetEncoded();
        trusted = new Dictionary<string, byte[]> { { LicenseProtocol.KeyId(pub), pub } };
        using (var device = CngKey.Create(CngAlgorithm.ECDsaP256))
        using (var other = CngKey.Create(CngAlgorithm.ECDsaP256))
        {
            string blob = Convert.ToBase64String(device.Export(CngKeyBlobFormat.EccPublicBlob));
            var o = new JObject { { "version", 3 }, { "licenseId", Guid.NewGuid().ToString("D") }, { "product", "Lumivolt" }, { "name", "테스트" }, { "issuedUtc", LicenseProtocol.Utc(now.AddSeconds(-2)) }, { "expiresUtc", LicenseProtocol.Utc(now.AddDays(30)) }, { "perpetual", false }, { "features", new JArray("automation") }, { "devicePublicKey", blob }, { "keyId", LicenseProtocol.KeyId(pub) }, { "status", "active" } };
            string good = Sign(o);
            Check("valid Ed25519 license", (string)Verify(good)["name"] == "테스트");
            var p = (JObject)o.DeepClone(); p["name"] = "tampered";
            byte[] data, sig; LicenseProtocol.Decode(good, "LV3", out data, out sig);
            Reject("payload tamper", () => Verify(LicenseProtocol.Wrap("LV3", LicenseProtocol.Bytes(p), sig)));
            sig[0] ^= 1; Reject("signature tamper", () => Verify(LicenseProtocol.Wrap("LV3", data, sig)));
            Reject("unsigned payload", () => Verify(LicenseProtocol.Wrap("LV3", data, new byte[64])));
            Reject("public key cannot forge signature", () => Verify(LicenseProtocol.Wrap("LV3", data, pub.Concat(pub).ToArray())));
            Action<string, JToken, string> mutation = (field, value, name) => { var m = (JObject)o.DeepClone(); m[field] = value; Reject(name, () => Verify(Sign(m))); };
            mutation("product", "OtherProduct", "different product"); mutation("version", 4, "unsupported version");
            mutation("version", "3", "wrong field type"); mutation("expiresUtc", LicenseProtocol.Utc(now.AddDays(-1)), "expired");
            mutation("issuedUtc", LicenseProtocol.Utc(now.AddHours(1)), "issued in future"); mutation("expiresUtc", JValue.CreateNull(), "missing expiry");
            mutation("features", new JArray("admin"), "unsupported feature"); mutation("features", new JArray(), "missing automation entitlement");
            mutation("features", new JArray("automation", "automation"), "duplicate entitlement"); mutation("status", "revoked", "inactive license");
            mutation("keyId", new string('0', 32), "unknown issuer"); mutation("devicePublicKey", Convert.ToBase64String(new byte[72]), "invalid device point");
            mutation("licenseId", "not-a-uuid", "invalid license ID"); mutation("name", new string('x',121), "oversized name");
            Reject("oversized file", () => Verify(new string('x', 40000))); Reject("invalid base64", () => Verify("LV3.?.?"));
            var extra = (JObject)o.DeepClone(); extra["extra"] = true; Reject("unknown field", () => Verify(Sign(extra)));
            var dup = LicenseProtocol.Utf8.GetString(data).Replace("\"version\":3", "\"version\":3,\"version\":3"); Reject("duplicate JSON field", () => Verify(Sign(LicenseProtocol.Utf8.GetBytes(dup))));
            Reject("trailing JSON", () => Verify(Sign(LicenseProtocol.Utf8.GetBytes(LicenseProtocol.Utf8.GetString(data) + "{}"))));
            p = (JObject)o.DeepClone(); p["perpetual"] = true; p["expiresUtc"] = JValue.CreateNull(); Check("perpetual license", (bool)Verify(Sign(p))["perpetual"]);
            var pretty = LicenseProtocol.Utf8.GetBytes(o.ToString()); Check("original noncanonical bytes are verified", Verify(Sign(pretty)) != null);
            using (var e = new ECDsaCng(device) { HashAlgorithm = CngAlgorithm.Sha256 })
            {
                var challenge = new byte[32]; new SecureRandom().NextBytes(challenge); var proof = e.SignData(challenge);
                Check("correct device proof", LicenseProtocol.DeviceProof(device.Export(CngKeyBlobFormat.EccPublicBlob), challenge, proof));
                Check("different device proof rejected", !LicenseProtocol.DeviceProof(other.Export(CngKeyBlobFormat.EccPublicBlob), challenge, proof));
                challenge[0] ^= 1; Check("replayed proof rejected", !LicenseProtocol.DeviceProof(device.Export(CngKeyBlobFormat.EccPublicBlob), challenge, proof));
            }
            string request = Request(device); Check("registration request proof", LicenseProtocol.VerifyRequest(request, now) != null);
            LicenseProtocol.Decode(request, "LVR3", out data, out sig); data[data.Length-5] ^= 1; Reject("request tamper rejected", () => LicenseProtocol.VerifyRequest(LicenseProtocol.Wrap("LVR3", data, sig), now));
            Reject("stale request rejected", () => LicenseProtocol.VerifyRequest(request, now.AddDays(31)));
            Reject("future request rejected", () => LicenseProtocol.VerifyRequest(request, now.AddDays(-1)));
            Reject("legacy license rejected", () => Verify("BJA2.fake"));
            string temp = Path.Combine(Path.GetTempPath(), "LumivoltIssuerTest-" + Guid.NewGuid().ToString("N"));
            string previous = IssuerService.LastVault;
            try
            {
                IssuerService.Create(temp); Check("DPAPI private vault", File.Exists(Path.Combine(temp, "issuer.dpapi")));
                Reject("no silent issuer overwrite", () => IssuerService.Create(temp));
                var issued = IssuerService.Issue(temp, request, "테스트", now.AddDays(1));
                Check("issuer round trip", (string)IssuerService.Inspect(temp, issued)["name"] == "테스트");
                IssuerService.ExportRecords(temp, Path.Combine(temp, "export.json")); Check("issuance history export", JArray.Parse(File.ReadAllText(Path.Combine(temp, "export.json"))).Count == 1);
                var backup = Path.Combine(temp, "backup.lvbackup"); var restored = Path.Combine(temp, "restored");
                string password = Guid.NewGuid().ToString("N");
                IssuerService.Backup(temp, backup, password);
                Reject("wrong backup password", () => IssuerService.Restore(backup, restored, "Wrong-password-123"));
                IssuerService.Restore(backup, restored, password);
                Check("encrypted backup restore retains issuer", (string)IssuerService.Public(temp)["keyId"] == (string)IssuerService.Public(restored)["keyId"]);
                Check("restored key verifies old licenses", IssuerService.Inspect(restored, issued) != null);
                var damaged = JObject.Parse(File.ReadAllText(backup)); var ct = Convert.FromBase64String((string)damaged["ciphertext"]); ct[0] ^= 1; damaged["ciphertext"] = Convert.ToBase64String(ct); File.WriteAllText(backup, damaged.ToString());
                Reject("tampered encrypted backup rejected", () => IssuerService.Restore(backup, Path.Combine(temp, "badrestore"), password));
                byte[] protectedKey = File.ReadAllBytes(Path.Combine(temp, "issuer.dpapi")); protectedKey[protectedKey.Length-1] ^= 1; File.WriteAllBytes(Path.Combine(temp, "issuer.dpapi"), protectedKey);
                Reject("damaged private vault rejected", () => IssuerService.Load(temp));
            }
            finally { IssuerService.LastVault = previous; Directory.Delete(temp, true); }
        }
        Console.WriteLine("SOFTWARE CRYPTOGRAPHY TESTS: " + passed + " passed, " + failures + " failed. Software device keys above are NOT physical TPM evidence.");
        string tpmRequest = null;
        try { tpmRequest = TpmDevice.CreateRequest(); }
        catch (Exception ex) { Console.WriteLine("TPM UNAVAILABLE / HARDWARE VALIDATION PENDING: " + TpmDevice.Error(ex)); }
        if (tpmRequest != null) try
        {
            var req = LicenseProtocol.VerifyRequest(tpmRequest, DateTime.UtcNow);
            TpmDevice.Prove((string)req["devicePublicKey"]); Check("platform provider device ownership proof", true);
            using (var k = TpmDevice.Open(false)) { Check("provider + nonexportable policy", k.Provider.Provider == "Microsoft Platform Crypto Provider" && k.ExportPolicy == CngExportPolicies.None); Reject("TPM private export denied", () => k.Export(CngKeyBlobFormat.EccPrivateBlob)); }
            Check("persistent key reopen", TpmDevice.DisplayId().Length == 16);
        }
        catch (Exception ex) { Check("TPM proof after successful key open: " + TpmDevice.Error(ex), false); }
        Console.WriteLine("TOTAL: " + passed + " passed, " + failures + " failed"); return failures == 0 ? 0 : 1;
    }
}
