#if UIPREVIEW
// 개발 전용(출시용 exe 에는 포함되지 않음): Xvfb 같은 가상 화면에서 각 페이지를 그려 PNG 로 저장해 레이아웃을 점검한다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace BunjangAuto
{
    public static class PreviewHarness
    {
        public static void Run(string[] args)
        {
            string outDir = args.Length > 1 ? args[1] : "/tmp/preview";
            string theme = args.Length > 2 ? args[2] : "light";
            int w = args.Length > 3 ? int.Parse(args[3]) : 1240, h = args.Length > 4 ? int.Parse(args[4]) : 820;
            System.IO.Directory.CreateDirectory(outDir);
            Application.EnableVisualStyles();
            Paths.EnsureAll();

            var cfg = Storage.LoadSettings(); cfg.ThemeMode = theme == "dark" ? "Dark" : "Light"; cfg.AssumeLoggedIn = true; Storage.SaveSettings(cfg);
            var prods = new System.ComponentModel.BindingList<Product>();
            string[][] rows = {
                new[]{"아이폰 15 프로 256GB 풀박스","1050000","디지털>휴대폰>애플","사용감 적음","등록됨","1234567"},
                new[]{"다이소 무선 이어폰 미개봉","8000","디지털>오디오>이어폰","새 상품(미사용)","",""},
                new[]{"나이키 에어포스 270 😀","89000","패션잡화>신발>스니커즈","사용감 많음","실패: 이미지 업로드에 실패했습니다",""},
                new[]{"레고 테크닉 42115","310000","키덜트>레고>테크닉","새 상품(미사용)","",""},
                new[]{"캐논 EOS R6 바디","1750000","디지털>카메라>미러리스","사용감 적음","등록됨","7654321"},
            };
            foreach (var r in rows) prods.Add(new Product { Title = r[0], Price = r[1], Category = r[2], Condition = r[3], Status = r[4], ProductId = r[5], Quantity = "1", Tags = "중고,직거래", Description = "설명입니다", FreeShipping = true, LastPosted = r[5] == "" ? "" : DateTime.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm:ss") });
            Storage.SaveProducts(prods);
            UsageMetricsService.Load();
            foreach (var m in new[] { "프로그램을 시작했습니다. 데이터 폴더: " + Paths.Root, "[등록] 아이폰 15 프로 256GB 풀박스 (1/3)", "  → 등록 완료", "[등록] 나이키 에어포스 270 (2/3)", "  → 실패: 이미지 업로드에 실패했습니다", "[1개씩 등록] 다음 등록 예정: 14:20:05", "CSV 5건을 불러왔습니다: C:\\상품목록.csv", "[주의] 데모 경고 문구" })
                Log.Write(m);
            LicenseGate.LastInfo = new LicenseInfo { Valid = true, Name = "데모 사용자", Expires = DateTime.Now.AddDays(12), Message = "만료일" };

            var f = new MainForm { StartPosition = FormStartPosition.Manual, Left = 0, Top = 0, Width = w, Height = h };
            f.Show();
            Pump(600);
            string[] pages = { "dashboard", "products", "automation", "login", "schedule", "logs", "help", "settings", "license" };
            foreach (var p in pages)
            {
                f.Navigate(p); Pump(700);
                Shot(System.IO.Path.Combine(outDir, theme + "-" + w + "-" + p + ".png"));
            }
            // 예전 대화상자(상품 편집 / 라이선스 / 이용약관)도 새 테마로 그려지는지 확인
            ShowDialogShot(new EditForm(prods[0]), System.IO.Path.Combine(outDir, theme + "-dlg-edit.png"));
            ShowDialogShot(new LicenseForm(License.GetMachineId(), "라이선스 키를 입력해 주세요."), System.IO.Path.Combine(outDir, theme + "-dlg-license.png"));
            ShowDialogShot(new TermsForm(), System.IO.Path.Combine(outDir, theme + "-dlg-terms.png"));
            f.Close();
        }

        private static void ShowDialogShot(Form d, string path)
        {
            d.StartPosition = FormStartPosition.Manual; d.Left = 20; d.Top = 20;
            d.Show(); Pump(700); Shot(path); d.Close(); d.Dispose();
        }

        private static void Pump(int ms)
        {
            var end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(20); }
        }

        private static void Shot(string path)
        {
            try
            {
                var psi = new ProcessStartInfo("import", "-window root \"" + path + "\"") { UseShellExecute = false };
                var p = Process.Start(psi); p.WaitForExit(15000);
            }
            catch (Exception ex) { Console.WriteLine("shot failed: " + ex.Message); }
        }
    }
}
#endif
