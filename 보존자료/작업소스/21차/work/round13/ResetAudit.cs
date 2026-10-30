using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Security.Cryptography;
using BunjangAuto;
class ResetAudit {
    static string directory;
    [STAThread] static int Main(string[] args) {
        directory=Path.GetDirectoryName(Path.GetFullPath(args[0]));
        AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){var app=Assembly.LoadFile(Path.Combine(directory,"EXP.exe"));using(var stream=app.GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(stream==null)return null;using(var memory=new MemoryStream()){stream.CopyTo(memory);return Assembly.Load(memory.ToArray());}}};
        return Run(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args){
        string data=File.ReadAllText(Paths.Products);if(Vault.IsProtected(data))data=Vault.Unprotect(data,"products");
        Console.WriteLine("ProductsReadable="+Newtonsoft.Json.Linq.JArray.Parse(data).Count);
        if(args.Length==1){var provider=new CngProvider("Microsoft Platform Crypto Provider");Console.WriteLine("DeviceKeyExists="+CngKey.Exists("Lumivolt.Device.v3",provider));Console.WriteLine("UsageMarkerExists="+CngKey.Exists("EXP.Usage.v1",provider));return 0;}
        bool oldRejected=!License.Verify(File.ReadAllText(args[1]),"").Valid;
        var master=Assembly.LoadFrom(Path.Combine(directory,"EXP-Master.exe"));Application.EnableVisualStyles();
        bool match;
        using(var form=(Form)Activator.CreateInstance(master.GetType("AdminForm"),true))
        using(var stream=typeof(Brand).Assembly.GetManifestResourceStream("brand.app.ico"))
        using(var appIcon=new Icon(stream))
        using(var a=appIcon.ToBitmap())using(var b=form.Icon.ToBitmap()) {
            match=a.Size==b.Size;
            if(match)for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))match=false;
        }
        Console.WriteLine((oldRejected?"PASS ":"FAIL ")+"old issuer license rejected by new release");
        Console.WriteLine((match?"PASS ":"FAIL ")+"Master window icon matches customer application icon");
        return oldRejected&&match?0:1;
    }
}
