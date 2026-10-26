using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BunjangAuto;
class GateIntegration
{
    static int failed;
    static void Check(string name, bool result) { Console.WriteLine((result ? "PASS " : "FAIL ") + name); if (!result) failed++; }
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) { using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } } };
        return Run(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
        Paths.EnsureAll(); string good = File.ReadAllText(args[0]), message;
        Check("activation stores verified license", LicenseGate.TryActivate(good, out message));
        Check("saved activation recheck", LicenseGate.Recheck());
        Check("copied diagnostic ID grants no bypass", !License.Verify("invalid", License.GetMachineId()).Valid);
        File.WriteAllText(Path.Combine(Paths.Root, "license.key"), "invalid");
        Check("corrupt license fails closed", !LicenseGate.Recheck());
        Check("restore valid license", LicenseGate.TryActivate(good, out message));
        var clock = Path.Combine(Paths.Root, "license.clock.v3"); var saved = File.ReadAllBytes(clock);
        File.WriteAllBytes(clock, new byte[30]);
        Check("clock tamper blocks recheck", !LicenseGate.Recheck());
        Check("clock tamper blocks activation", !LicenseGate.TryActivate(good, out message));
        File.WriteAllBytes(clock, ProtectedData.Protect(Lumivolt.Security.LicenseProtocol.Utf8.GetBytes(Lumivolt.Security.LicenseProtocol.Utc(DateTime.UtcNow.AddDays(2))), Lumivolt.Security.LicenseProtocol.Utf8.GetBytes("Lumivolt.Clock.v3"), DataProtectionScope.CurrentUser));
        Check("clock rollback blocks activation", !LicenseGate.TryActivate(good, out message));
        File.WriteAllBytes(clock, saved); Check("restored valid clock", LicenseGate.Recheck());
        File.Delete(clock); Directory.CreateDirectory(clock);
        Check("clock write failure cannot authorize", !LicenseGate.TryActivate(good, out message));
        Directory.Delete(clock); File.WriteAllBytes(clock, saved);
        Check("other device signed license rejected", !License.Verify(File.ReadAllText(args[1]), License.GetMachineId()).Valid);
        Console.WriteLine("GATE FAILURES: " + failed); return failed == 0 ? 0 : 1;
    }
}
