using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using BunjangAuto;
class AdminUiSmoke
{
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) { using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } } };
        return Run(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
        AppFont.Init(); Application.EnableVisualStyles();
        using (var f = new AdminForm())
        {
            f.ShowInTaskbar = false; f.Opacity = 0; f.Show(); Application.DoEvents();
            using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(args[0]); }
        }
        Console.WriteLine("PASS administrator UI rendered"); return 0;
    }
}
