using System;
using System.IO;
using System.Reflection;
using BunjangAuto;
class ProductionSmoke
{
    static int Main(string[] args)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[0]));
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            var app = Assembly.LoadFile(Path.Combine(directory, "Lumivolt.exe"));
            using (var s = app.GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } }
        };
        return Run(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
        string token = File.ReadAllText(args[1]);
        var good = License.Verify(token, "COPIED-DIAGNOSTIC-ID");
        Console.WriteLine((good.Valid ? "PASS " : "FAIL ") + "production EXE Ed25519 + TPM validation: " + good.Message);
        var bad = License.Verify(token.Substring(0, token.Length-4) + "AAAA", License.GetMachineId());
        Console.WriteLine((!bad.Valid ? "PASS " : "FAIL ") + "production EXE tamper rejection");
        var asm = typeof(License).Assembly;
        bool clean = asm.GetType("Lumivolt.Admin.IssuerService") == null && asm.GetType("SecurityTests") == null && asm.GetType("BunjangAuto.PreviewHarness") == null && typeof(License).GetMethod("Sign") == null;
        Console.WriteLine((clean ? "PASS " : "FAIL ") + "no issuer/signing helper/preview harness in customer assembly");
        return good.Valid && !bad.Valid && clean ? 0 : 1;
    }
}
