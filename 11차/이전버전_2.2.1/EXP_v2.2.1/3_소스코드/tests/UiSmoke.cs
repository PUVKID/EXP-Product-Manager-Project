using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using BunjangAuto;
class UiSmoke
{
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } }
        };
        try { Run(args); return 0; } catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string[] args)
    {
        string target = Path.GetFullPath(args[0]); Directory.CreateDirectory(target);
        AppFont.Init(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Paths.EnsureAll();
        foreach (var mode in new[] { "Light", "Dark" })
        {
            var cfg = new AppSettings { ThemeMode = mode }; Storage.SaveSettings(cfg); ThemeService.Init(cfg);
            using (var form = new MainForm())
            {
                foreach (var page in new[] { "dashboard", "products", "automation", "login", "schedule", "logs", "help", "settings", "license" })
                { form.Navigate(page); Render(form, Path.Combine(target, mode + "-" + page + ".png")); }
            }
            using (var form = new LicenseForm("TPM-DEVICE-ID", "라이선스를 등록해 주세요.")) Render(form, Path.Combine(target, mode + "-license-dialog.png"));
        }
        Console.WriteLine("PASS UI render: 20 Windows forms/pages; no browser automation or publishing executed.");
    }
    static void Render(Form f, string path)
    {
        if (!f.Visible) { f.ShowInTaskbar = false; f.Opacity = 0; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); f.Show(); }
        Application.DoEvents(); f.PerformLayout();
        using (var bitmap = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bitmap, new Rectangle(0, 0, f.Width, f.Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
    }
}
