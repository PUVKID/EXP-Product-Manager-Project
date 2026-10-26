using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BunjangAuto
{
    /// <summary>설치별 Client ID(랜덤 UUID)와 관리자 화면에 보여 줄 시스템 정보를 만든다.</summary>
    public static class ClientIdentityService
    {
        private static string _id;
        private static readonly object _gate = new object();

        public static string Path_ { get { return Path.Combine(Paths.Root, "client.id"); } }

        /// <summary>처음 한 번 만든 뒤 %AppData%\BunjangAuto\client.id 에 저장해 재사용한다. 하드웨어/계정 정보와 무관한 순수 랜덤 값.</summary>
        public static string ClientId
        {
            get
            {
                lock (_gate)
                {
                    if (_id != null) return _id;
                    try
                    {
                        if (File.Exists(Path_))
                        {
                            string t = File.ReadAllText(Path_, Encoding.UTF8).Trim();
                            Guid g;
                            if (Guid.TryParse(t, out g)) { _id = g.ToString(); return _id; }
                        }
                    }
                    catch { }
                    _id = Guid.NewGuid().ToString();
                    try { Directory.CreateDirectory(Paths.Root); File.WriteAllText(Path_, _id, Encoding.UTF8); } catch { }
                    return _id;
                }
            }
        }

        /// <summary>테스트용: 저장된 ID 캐시를 비운다.</summary>
        public static void ResetCache() { lock (_gate) { _id = null; } }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MemoryStatusEx
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

        public static string AppVersion
        {
            get
            {
                try { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); } catch { return "0.0.0.0"; }
            }
        }

        public static string OsDescription()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        string name = Convert.ToString(k.GetValue("ProductName"));
                        string disp = Convert.ToString(k.GetValue("DisplayVersion"));
                        if (string.IsNullOrEmpty(disp)) disp = Convert.ToString(k.GetValue("ReleaseId"));
                        string build = Convert.ToString(k.GetValue("CurrentBuild"));
                        int b; int.TryParse(build, out b);
                        if (b >= 22000 && name.StartsWith("Windows 10")) name = "Windows 11" + name.Substring("Windows 10".Length);   // 레지스트리는 Win11 에서도 10 으로 적혀 있다
                        if (!string.IsNullOrEmpty(name))
                            return (name + (string.IsNullOrEmpty(disp) ? "" : " " + disp) + (b > 0 ? " (빌드 " + build + ")" : "")).Trim();
                    }
                }
            }
            catch { }
            return Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " x64" : " x86");
        }

        public static string CpuDescription()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    if (k != null) { string n = Convert.ToString(k.GetValue("ProcessorNameString")).Trim(); if (n.Length > 0) return n + " (" + Environment.ProcessorCount + "스레드)"; }
            }
            catch { }
            string e = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            return (e ?? "unknown") + " (" + Environment.ProcessorCount + "스레드)";
        }

        public static int RamMb()
        {
            try { var m = new MemoryStatusEx(); if (GlobalMemoryStatusEx(m)) return (int)(m.ullTotalPhys / (1024UL * 1024UL)); } catch { }
            return 0;
        }

        public static string GpuDescription()
        {
            try
            {
                var names = new List<string>();
                using (var s = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (System.Management.ManagementObject o in s.Get()) { var n = Convert.ToString(o["Name"]); if (!string.IsNullOrEmpty(n)) names.Add(n); }
                if (names.Count > 0) return string.Join(" / ", names.ToArray());
            }
            catch { }
            return "";
        }

        public static string ScreenDescription()
        {
            try { var b = Screen.PrimaryScreen.Bounds; return b.Width + "x" + b.Height; } catch { return ""; }
        }

        public static int Dpi()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return (int)Math.Round(g.DpiX); } catch { return 96; }
        }

        /// <summary>서버에 알리는 시스템 정보. (비밀정보·계정정보·파일 내용은 포함하지 않는다.)</summary>
        public static Dictionary<string, object> Describe()
        {
            var d = new Dictionary<string, object>();
            d["app_version"] = AppVersion;
            d["os"] = OsDescription();
            d["cpu"] = CpuDescription();
            d["ram_mb"] = RamMb();
            d["gpu"] = GpuDescription();
            d["screen"] = ScreenDescription();
            d["dpi"] = Dpi();
            try { d["pc_id"] = License.GetMachineId(); } catch { }
            var li = LicenseGate.LastInfo;
            if (li != null && li.Valid)
            {
                d["license_name"] = li.Name;
                d["license_expires"] = li.Expires == DateTime.MaxValue ? "0" : li.Expires.ToString("yyyy-MM-dd");
            }
            return d;
        }
    }
}
