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
        foreach (var dark in new[] { false, true }) using (var f = new AdminForm())
        {
            Theme.Set(dark ? ThemePalette.DarkPal : ThemePalette.Light); Theme.Apply(f);
            f.ShowInTaskbar = false; f.Opacity = 0; f.Show(); Application.DoEvents();
            var mode=(ComboBox)typeof(AdminForm).GetField("mode",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
            for(int i=0;i<3;i++) { mode.SelectedIndex=i; Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(args[0]+(dark?"-Dark":"-Light")+"-"+i+".png"); }
            }
        }
        Console.WriteLine("PASS administrator UI rendered"); return 0;
    }
}
