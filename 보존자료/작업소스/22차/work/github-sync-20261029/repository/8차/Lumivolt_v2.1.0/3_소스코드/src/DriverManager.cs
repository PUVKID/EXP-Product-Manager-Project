﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    /// <summary>
    /// 설치된 Chrome 버전에 맞는 chromedriver.exe 를 자동으로 찾거나 내려받는다.
    /// (exe 하나만 배포해도 동작하게 하는 핵심 부분)
    /// </summary>
    public static class DriverManager
    {
        public static string FindChromeExe(string custom)
        {
            if (!string.IsNullOrEmpty(custom) && File.Exists(custom)) return custom;
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                {
                    if (k != null)
                    {
                        string v = k.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(v) && File.Exists(v)) return v;
                    }
                }
            }
            catch { }
            return null;
        }

        public static string GetChromeVersion(string chromeExe)
        {
            try
            {
                if (chromeExe != null && File.Exists(chromeExe))
                {
                    string v = FileVersionInfo.GetVersionInfo(chromeExe).FileVersion;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Google\Chrome\BLBeacon"))
                {
                    if (k != null) { string v = k.GetValue("version") as string; if (!string.IsNullOrEmpty(v)) return v; }
                }
            }
            catch { }
            return null;
        }

        /// <summary>사용할 chromedriver.exe 경로를 돌려준다. 없으면 내려받는다.</summary>
        public static string EnsureDriver(string chromeExe, Action<string> log)
        {
            string ver = GetChromeVersion(chromeExe);
            if (ver == null)
                throw new InvalidOperationException("Chrome 이 설치되어 있지 않은 것 같습니다. Chrome 을 먼저 설치해 주세요.");
            string major = ver.Split('.')[0];
            string build = string.Join(".", ver.Split('.'), 0, Math.Min(3, ver.Split('.').Length));
            log("설치된 Chrome 버전: " + ver);

            // 1) exe 옆에 chromedriver.exe 가 있고 메이저 버전이 같으면 그걸 사용
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromedriver.exe");
            if (File.Exists(beside) && DriverMajor(beside) == major)
            {
                log("exe 옆의 chromedriver 사용: " + beside);
                return beside;
            }

            // 2) 캐시
            string dir = Path.Combine(Paths.Drivers, major);
            string cached = Path.Combine(dir, "chromedriver.exe");
            if (File.Exists(cached) && DriverMajor(cached) == major) return cached;

            // 3) 다운로드
            log("chromedriver " + major + " 버전을 내려받는 중...");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string url = ResolveDownloadUrl(build, major);
            if (url == null) throw new InvalidOperationException("Chrome " + ver + " 에 맞는 chromedriver 를 찾지 못했습니다.");

            Directory.CreateDirectory(dir);
            string zip = Path.Combine(Path.GetTempPath(), "chromedriver-" + Guid.NewGuid().ToString("N") + ".zip");
            using (var wc = new WebClient())
            {
                wc.Headers["User-Agent"] = "Lumivolt/2.0";
                wc.DownloadFile(url, zip);
            }
            try
            {
                using (var za = ZipFile.OpenRead(zip))
                {
                    foreach (var e in za.Entries)
                    {
                        if (e.FullName.EndsWith("/chromedriver.exe", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.Equals("chromedriver.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            e.ExtractToFile(cached, true);
                            break;
                        }
                    }
                }
            }
            finally { try { File.Delete(zip); } catch { } }

            if (!File.Exists(cached)) throw new InvalidOperationException("chromedriver 압축 해제에 실패했습니다.");
            log("chromedriver 준비 완료");
            return cached;
        }

        private static string ResolveDownloadUrl(string build, string major)
        {
            string plat = Environment.Is64BitOperatingSystem ? "win64" : "win32";
            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers["User-Agent"] = "Lumivolt/2.0";
                // 빌드(앞 3자리)까지 같은 가장 최신 패치
                try
                {
                    var j = JObject.Parse(wc.DownloadString("https://googlechromelabs.github.io/chrome-for-testing/latest-patch-versions-per-build-with-downloads.json"));
                    var node = j["builds"] == null ? null : j["builds"][build];
                    string u = PickUrl(node, plat);
                    if (u != null) return u;
                }
                catch { }
                // 메이저 버전 기준 최신
                try
                {
                    var j = JObject.Parse(wc.DownloadString("https://googlechromelabs.github.io/chrome-for-testing/latest-versions-per-milestone-with-downloads.json"));
                    var node = j["milestones"] == null ? null : j["milestones"][major];
                    string u = PickUrl(node, plat);
                    if (u != null) return u;
                }
                catch { }
            }
            return null;
        }

        private static string PickUrl(JToken node, string plat)
        {
            if (node == null) return null;
            var arr = node["downloads"] == null ? null : node["downloads"]["chromedriver"] as JArray;
            if (arr == null) return null;
            foreach (var it in arr)
                if ((string)it["platform"] == plat) return (string)it["url"];
            return null;
        }

        private static string DriverMajor(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    var m = Regex.Match(o, @"ChromeDriver\s+(\d+)\.");
                    return m.Success ? m.Groups[1].Value : null;
                }
            }
            catch { return null; }
        }
    }
}
