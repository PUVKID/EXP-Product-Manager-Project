#if UIPREVIEW
// 개발 전용: 화면 배율(125~200%)을 흉내 내서 모든 페이지를 띄우고, 컨트롤이 부모 밖으로 나가거나 서로 겹치거나 글자가 잘리는지 검사한다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BunjangAuto
{
    public static class UiLayoutCheck
    {
        private static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); } }
        private static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
        private static IEnumerable<Control> All(Control c) { yield return c; foreach (Control k in c.Controls) foreach (var x in All(k)) yield return x; }
        private static int TW(string t, Font f) { return TextRenderer.MeasureText(t ?? "", f, new Size(10000, 200), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width; }
        private static Rectangle Abs(Control c) { return c.Parent == null ? c.Bounds : new Rectangle(c.Parent.PointToScreen(c.Location), c.Size); }

        public static int Run(string[] args)
        {
            double scale = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1.0;
            string theme = args.Length > 2 ? args[2] : "light"; string shots = args.Length > 3 ? args[3] : null;
            Dpi.ForcedScale = scale; Dpi.EmulateFontScale = scale > 1.001;
            Application.EnableVisualStyles(); Paths.EnsureAll();
            var cfg = Storage.LoadSettings(); cfg.ThemeMode = theme == "dark" ? "Dark" : "Light"; cfg.AssumeLoggedIn = true; Storage.SaveSettings(cfg);
            var seed = new System.ComponentModel.BindingList<Product>();
            for (int i = 0; i < 6; i++) seed.Add(new Product { Title = "아이폰 15 프로 256GB 풀박스 미개봉 새상품 " + i, Price = "1050000", Category = "디지털>휴대폰>애플", Condition = "사용감 적음", Quantity = "1", Status = i % 3 == 0 ? "등록됨" : i % 3 == 1 ? "실패: 이미지 업로드에 실패했습니다" : "", ProductId = i % 3 == 0 ? "12345" : "" });
            Storage.SaveProducts(seed);
            LicenseGate.LastInfo = new LicenseInfo { Valid = true, Name = "데모 사용자", Expires = DateTime.Now.AddDays(12), Message = "ok" };
            UsageMetricsService.Load();
            foreach (var m in new[] { "프로그램을 시작했습니다. 데이터 폴더: " + Paths.Root, "[등록] 아이폰 15 프로 256GB 풀박스 (1/3)", "  → 등록 완료", "  → 실패: 이미지 업로드에 실패했습니다", "[1개씩 등록] 다음 등록 예정: 14:20:05" }) Log.Write(m);

            int W = (int)(1240 * scale), H = (int)(820 * scale);
            var f = new MainForm { StartPosition = FormStartPosition.Manual, Left = 0, Top = 0, Width = W, Height = H };
            f.Show(); Pump(700);
            var pages = Field<Dictionary<string, PageBase>>(f, "pages");
            var navs = Field<Dictionary<string, NavItem>>(f, "navs");
            var issues = new List<string>();
            Action<string> bad = s => issues.Add(s);

            // ---- 사이드바 / 헤더
            var sidebar = Field<Control>(f, "sidebar"); var header = Field<Control>(f, "header"); var lblFoot = Field<Label>(f, "lblFoot");
            var vis = navs.Values.Where(n => n.Visible).OrderBy(n => n.Top).ToList();
            for (int i = 0; i < vis.Count; i++)
            {
                var n = vis[i];
                if (i > 0 && n.Top < vis[i - 1].Bottom) bad("사이드바 항목 겹침: " + vis[i - 1].Text + "/" + n.Text);
                if (TW(n.Text, n.Font) > n.Width - Dpi.S(62)) bad("사이드바 글자 잘림: " + n.Text);
                if (n.Bottom > lblFoot.Top) bad("사이드바 항목이 하단 문구와 겹침: " + n.Text);
            }
            var lblPage = Field<Label>(f, "lblPage"); var chipLic = Field<Chip>(f, "chipLic"); var chipUser = Field<Chip>(f, "chipUser"); var seg = Field<Segmented>(f, "segTheme");
            if (lblPage.Right > chipLic.Left) bad("헤더: 페이지 제목과 라이선스 칩 겹침");
            if (chipLic.Right > chipUser.Left || chipUser.Right > seg.Left) bad("헤더 칩/테마 선택 겹침");
            if (seg.Right > header.Width) bad("헤더 오른쪽 밖으로 나감");
            foreach (var it in seg.Items) if (TW(it, seg.Font) > seg.Width / seg.Items.Count - Dpi.S(10)) bad("테마 선택 글자 잘림: " + it);

            foreach (var key in new[] { "dashboard", "products", "automation", "login", "schedule", "logs", "help", "settings", "license" })
            {
                f.Navigate(key); Pump(500);
                var page = pages[key]; var list = new List<string>();
                foreach (var c in All(page))
                {
                    if (!c.Visible || c == page || c.Parent == null) continue;
                    var p = c.Parent;
                    if (c.Dock == DockStyle.None)
                    {
                        // 부모 안에 들어 있는가 (가로는 항상, 세로는 스크롤 영역이 아닌 부모만)
                        bool scroll = (p is ScrollableControl) && ((ScrollableControl)p).AutoScroll;
                        if (c.Left < -1 || c.Right > p.ClientSize.Width + 2) list.Add(key + ": 가로로 부모 밖 → " + c.GetType().Name + (c is Label ? " '" + c.Text.Split('\n')[0] + "'" : "") + " [" + c.Left + ".." + c.Right + " / " + p.ClientSize.Width + "]");
                        if (!scroll && (c.Top < -1 || c.Bottom > p.ClientSize.Height + 2)) list.Add(key + ": 세로로 부모 밖 → " + c.GetType().Name + (c is Label ? " '" + c.Text.Split('\n')[0] + "'" : "") + " [" + c.Top + ".." + c.Bottom + " / " + p.ClientSize.Height + "]");
                    }
                    // 글자 잘림
                    if (c is FluentButton) { var b = (FluentButton)c; int need = TW(b.Text, b.Font) + (b.Glyph.Length > 0 && Theme.HasIconFont ? Dpi.S(26) : 0) + Dpi.S(16); if (need > b.Width) list.Add(key + ": 버튼 글자 잘림 '" + b.Text + "' 필요 " + need + " > " + b.Width); }
                    if (c is ToggleSwitch) { var t = (ToggleSwitch)c; if (TW(t.Text, t.Font) + Dpi.S(60) > t.Width) list.Add(key + ": 토글 글자 잘림 '" + t.Text + "'"); }
                    if (c is StatCard) { var s = (StatCard)c; if (TW(s.Value, Theme.Font(22f, FontStyle.Bold)) > s.Width - Dpi.S(32)) list.Add(key + ": 카드 숫자 잘림 " + s.Caption + "=" + s.Value); if (TW(s.Caption, Theme.Font(9f, FontStyle.Regular)) > s.Width - Dpi.S(32)) list.Add(key + ": 카드 제목 잘림 " + s.Caption); if (TW(s.Hint, Theme.Font(8.5f, FontStyle.Regular)) > s.Width - Dpi.S(32)) list.Add(key + ": 카드 설명 잘림(" + s.Caption + "): " + s.Hint); }
                }
                // 같은 부모 안의 상호작용 컨트롤끼리 겹침
                foreach (var grp in All(page).Where(c => c.Visible && c.Dock == DockStyle.None && c.Parent != null && (c is FluentButton || c is ToggleSwitch || c is TextBox || c is ComboBox || c is NumericUpDown || c is Chip || c is Segmented || c is Label)).GroupBy(c => c.Parent))
                {
                    var arr = grp.ToArray();
                    for (int i = 0; i < arr.Length; i++) for (int j = i + 1; j < arr.Length; j++)
                    {
                        var a = arr[i].Bounds; var b = arr[j].Bounds; var inter = Rectangle.Intersect(a, b);
                        if (inter.Width > Dpi.S(3) && inter.Height > Dpi.S(3)) list.Add(key + ": 겹침 " + Desc(arr[i]) + " ↔ " + Desc(arr[j]));
                    }
                }
                foreach (var l in list.Distinct()) bad(l);
                if (shots != null) { try { System.IO.Directory.CreateDirectory(shots); var psi = new ProcessStartInfo("import", "-window root \"" + System.IO.Path.Combine(shots, theme + "-" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "-" + key + ".png") + "\"") { UseShellExecute = false }; Process.Start(psi).WaitForExit(15000); } catch { } }
            }
            f.Close();
            Console.WriteLine("LAYOUT scale=" + scale + " theme=" + theme + ": " + (issues.Count == 0 ? "OK (문제 없음)" : issues.Count + "건"));
            foreach (var s in issues.Distinct()) Console.WriteLine("   ! " + s);
            return issues.Count == 0 ? 0 : 1;
        }

        private static string Desc(Control c) { return c.GetType().Name + (c is Label ? "'" + c.Text.Split('\n')[0] + "'" : c is FluentButton ? "'" + ((FluentButton)c).Text + "'" : c is ToggleSwitch ? "'" + ((ToggleSwitch)c).Text + "'" : "") + "@" + c.Left + "," + c.Top + " " + c.Width + "x" + c.Height; }
    }
}
#endif
