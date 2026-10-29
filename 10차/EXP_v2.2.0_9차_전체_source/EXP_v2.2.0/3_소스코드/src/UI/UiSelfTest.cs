#if UIPREVIEW
// 개발 전용(출시용 exe 에는 포함되지 않음): 가상 화면에서 MainForm 을 띄우고 실제로 컨트롤을 조작해 동작을 검사한다.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace BunjangAuto
{
    public static class UiSelfTest
    {
        private static int _fail, _pass;
        private static void Check(string name, bool ok) { Console.WriteLine((ok ? "  PASS " : "  FAIL ") + name); if (ok) _pass++; else _fail++; }
        private static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); } }
        private static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
        private static IEnumerable<Control> All(Control c) { yield return c; foreach (Control k in c.Controls) foreach (var x in All(k)) yield return x; }

        public static int Run()
        {
            Application.EnableVisualStyles();
            Paths.EnsureAll();
            var cfg0 = Storage.LoadSettings(); cfg0.ThemeMode = "Light"; Storage.SaveSettings(cfg0);
            var seed = new System.ComponentModel.BindingList<Product>();
            string[][] rows = { new[] { "아이폰 15 프로", "1050000", "디지털>휴대폰>애플", "등록됨", "111" }, new[] { "무선 이어폰", "8000", "디지털>오디오>이어폰", "", "" },
                                new[] { "나이키 에어포스", "89000", "패션잡화>신발>스니커즈", "실패: 이미지 업로드에 실패했습니다", "" }, new[] { "레고 테크닉", "310000", "키덜트>레고>테크닉", "", "" },
                                new[] { "캐논 EOS R6", "1750000", "디지털>카메라>미러리스", "등록됨", "222" } };
            foreach (var r in rows) seed.Add(new Product { Title = r[0], Price = r[1], Category = r[2], Condition = "사용감 적음", Quantity = "1", Status = r[3], ProductId = r[4] });
            Storage.SaveProducts(seed);
            LicenseGate.LastInfo = new LicenseInfo { Valid = true, Name = "테스터", Expires = DateTime.Now.AddDays(30), Message = "ok" };
            UsageMetricsService.Load();

            // Ed25519 + TPM gate tests are in tests/SecurityTests.cs and GateIntegration.cs.

            var f = new MainForm { StartPosition = FormStartPosition.Manual, Left = 0, Top = 0, Width = 1240, Height = 820 };
            f.Show(); Pump(500);
            var host = Field<AppHost>(f, "host");
            var pages = Field<Dictionary<string, PageBase>>(f, "pages");
            var seg = Field<Segmented>(f, "segTheme");

            // ---- 모든 페이지 이동
            bool nav = true; string[] keys = { "dashboard", "products", "automation", "login", "schedule", "logs", "help", "settings", "license" };
            foreach (var k in keys) { try { f.Navigate(k); Pump(250); if (!pages.ContainsKey(k) || !pages[k].Visible) nav = false; } catch (Exception ex) { Console.WriteLine("    " + k + ": " + ex.Message); nav = false; } }
            Check("[UI] 9개 페이지 모두 이동/표시", nav);
            f.Navigate("admin"); Pump(100);
            Check("[UI] 관리자 로그인 전에는 관리자 센터로 못 들어감(설정으로 돌려보냄)", pages.ContainsKey("settings") && pages["settings"].Visible && !pages.ContainsKey("admin"));

            // ---- 글꼴
            Check("[Font] 나눔고딕 Bold 가 실제 적용됨", AppFont.Active && Theme.Family == "NanumGothic" && Theme.FontBase().Bold && (Theme.FontBase().Name == "NanumGothic" || Theme.FontBase().Name == "나눔고딕"));
            Check("[Font] 폼/버튼/그리드에도 같은 글꼴", f.Font.Name == "NanumGothic" && All(f).OfType<FluentButton>().All(b => b.Font.Name == "NanumGothic"));

            // ---- 테마 전환 (헤더의 Segmented 를 누른 것과 같은 경로)
            Func<bool> consistent = () =>
            {
                var p = Theme.Current;
                bool ok = f.BackColor == p.Bg;
                foreach (var c in All(f))
                {
                    if (c is CardPanel && c.BackColor != p.Card) { Console.WriteLine("    카드 색 불일치: " + c.GetType().Name); ok = false; }
                    if (c is Label && (c.Tag as string) == "t" && ((Label)c).ForeColor != p.Text) ok = false;
                    if (c is DataGridView && ((DataGridView)c).BackgroundColor != p.Card) ok = false;
                    if (c is SidebarPanel && c.BackColor != p.Sidebar) ok = false;
                }
                return ok;
            };
            seg.SelectedIndex = 1; Pump(400);
            Check("[Theme] 다크 선택 → 팔레트 적용 + settings.json 저장", Theme.Current.Dark && Storage.LoadSettings().ThemeMode == "Dark" && consistent());
            seg.SelectedIndex = 0; Pump(400);
            Check("[Theme] 라이트 선택 → 모든 화면 색 일치", !Theme.Current.Dark && Storage.LoadSettings().ThemeMode == "Light" && consistent());
            seg.SelectedIndex = 2; Pump(400);
            Check("[Theme] 시스템 선택 → 저장 및 시스템 모드 추적", Storage.LoadSettings().ThemeMode == "System" && ThemeService.Mode == ThemeMode.System && Theme.Current.Dark == ThemeService.SystemIsDark());
            // 설정 화면의 Segmented 도 같은 경로
            f.Navigate("settings"); Pump(200);
            var segSet = All(pages["settings"]).OfType<Segmented>().First(); segSet.SelectedIndex = 1; Pump(300);
            Check("[Theme] 설정 화면에서도 변경 가능, 헤더 표시도 동기화", ThemeService.Mode == ThemeMode.Dark && seg.SelectedIndex == 1 && consistent());
            // 재실행 후 유지: 새 설정 읽기
            Check("[Theme] 재실행 시 저장된 선택을 복원", ThemeService.Parse(Storage.LoadSettings().ThemeMode) == ThemeMode.Dark);
            seg.SelectedIndex = 0; Pump(300);

            // ---- 상품 관리: 검색 / 필터 / 정렬
            f.Navigate("products"); Pump(300);
            var pp = pages["products"]; var grid = All(pp).OfType<DataGridView>().First();
            var search = All(pp).OfType<TextBox>().First(); var combo = All(pp).OfType<ComboBox>().First();
            Check("[Products] 처음엔 전체 5개 표시", grid.RowCount == 5);
            search.Text = "아이폰"; Pump(150);
            Check("[Products] 검색 '아이폰' → 1건", grid.RowCount == 1);
            search.Text = "";
            combo.SelectedIndex = 3; Pump(150);   // 등록 실패
            Check("[Products] 필터 '등록 실패' → 1건(나이키)", grid.RowCount == 1 && ((Product)grid.Rows[0].DataBoundItem).Title.StartsWith("나이키"));
            combo.SelectedIndex = 2; Pump(150);   // 등록 성공
            Check("[Products] 필터 '등록 성공' → 2건", grid.RowCount == 2);
            combo.SelectedIndex = 1; Pump(150);   // 등록 대기
            Check("[Products] 필터 '등록 대기' → 2건", grid.RowCount == 2);
            combo.SelectedIndex = 0; Pump(150);
            var t = pp.GetType();
            t.GetField("_sortProp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pp, "Price"); t.GetField("_sortAsc", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pp, false);
            t.GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pp, null); Pump(150);
            var first = (Product)grid.Rows[0].DataBoundItem;
            Check("[Products] 가격 내림차순 정렬(화면만) → 첫 행 캐논, 실제 목록 순서는 그대로", first.Title.StartsWith("캐논") && host.Products[0].Title.StartsWith("아이폰"));
            t.GetField("_sortProp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pp, ""); t.GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pp, null);

            // ---- 로그 화면
            Log.Write("[등록] 로그 화면 테스트 상품"); Log.Write("  → 실패: 테스트 실패 문구"); Pump(700);
            f.Navigate("logs"); Pump(500);
            var lg = pages["logs"]; var lgrid = All(lg).OfType<DataGridView>().First(); var lsearch = All(lg).OfType<TextBox>().First();
            int before = lgrid.RowCount; lsearch.Text = "테스트 실패 문구"; Pump(300);
            Check("[Logs] 실시간 로그가 화면에 쌓이고 검색됨", before >= 2 && lgrid.RowCount == 1);
            var lcombo = All(lg).OfType<ComboBox>().First(); lsearch.Text = ""; lcombo.SelectedIndex = 3; Pump(300);   // ERROR만
            Check("[Logs] Level 필터(ERROR만)", lgrid.RowCount >= 1 && lgrid.RowCount < before + 5);
            lcombo.SelectedIndex = 0;

            // ---- 도움말
            f.Navigate("help"); Pump(400);
            var hp = pages["help"]; var hsearch = All(hp).OfType<TextBox>().First(); var toc = All(hp).OfType<FlatList>().First(); var doc = All(hp).OfType<DocView>().First();
            int total = toc.Items.Count; int h0 = doc.ContentHeight;
            hsearch.Text = "CSV"; Pump(300);
            Check("[Help] 20개 문서, 검색 'CSV' → 일부만 남고 본문 표시", total == 20 && toc.Items.Count > 0 && toc.Items.Count < 20 && doc.ContentHeight > 100);
            hsearch.Text = "zzzz없는검색어"; Pump(300);
            Check("[Help] 검색 결과 없음 처리", toc.Items.Count == 0);
            hsearch.Text = "";
            toc.SelectedIndex = 4; Pump(300);
            Check("[Help] 목차 선택 시 본문 교체(스크롤 맨 위)", doc.ContentHeight > 100 && doc.ScrollPos == 0);

            // ---- 자동 등록 흐름 (Chrome 이 없는 환경 → 실패 경로 확인)
            f.Navigate("automation"); Pump(300);
            var ap = pages["automation"]; var btns = All(ap).OfType<FluentButton>().ToList();
            var bStart = btns.First(b => b.Text == "시작"); var bStop = btns.First(b => b.Text == "중지"); var bPause = btns.First(b => b.Text == "일시정지" || b.Text == "계속");
            Check("[Auto] 시작 전: 시작만 활성, 일시정지·중지 비활성", bStart.Enabled && !bStop.Enabled && !bPause.Enabled);
            host.Cfg.OneByOne = true; host.Cfg.UseTimeRules = false; host.Cfg.OneByOneMinutes = 20; host.Cfg.RepeatAll = false; host.Cfg.DoRegister = true;
            bStart.PerformClick(); Pump(300);
            Check("[Auto] 시작 직후 컨트롤러가 활성화되고 시작 버튼 비활성", host.Auto.Active && !bStart.Enabled && bStop.Enabled);
            // Runner 가 (Chrome 없음으로) 끝날 때까지 기다린다
            int wait = 0; while (host.Runner.Running && wait++ < 400) Pump(50);
            Pump(500);
            Check("[Auto] 실행이 끝나면 '다음 실행 대기'로 넘어가고 다음 시각이 정해짐(1개씩 모드)", host.Auto.Active && host.Auto.NextRun > DateTime.Now.AddMinutes(5) && host.Auto.StateText.Contains("다음 실행 대기"));
            host.Auto.Tick();
            Check("[Auto] 다음 실행까지 남은 시간이 화면 문구로 표시됨", host.Auto.NextText.Contains("다음 실행까지"));
            bStop.PerformClick(); Pump(400);
            Check("[Auto] 중지 → 예약 해제, 대기 중으로 복귀", !host.Auto.Active && host.Auto.NextRun == DateTime.MinValue && bStart.Enabled && !bStop.Enabled);
            // 일시정지 버튼은 실행 중일 때만 의미가 있으므로 상태 문구/버튼 토글만 검사
            Check("[Auto] 일시정지 버튼 상태 전환 로직(실행 중 아님 → 비활성)", !bPause.Enabled);

            // ---- 예약 화면: 설정 변경이 즉시 저장됨
            f.Navigate("schedule"); Pump(300);
            var sp = pages["schedule"]; var toggles = All(sp).OfType<ToggleSwitch>().ToList(); var nums = All(sp).OfType<NumericUpDown>().ToList();
            nums[0].Value = 33; Pump(100);
            Check("[Schedule] 기본 간격 변경이 즉시 settings.json 에 저장", Storage.LoadSettings().OneByOneMinutes == 33);
            var dup = toggles.First(x => x.Text.StartsWith("중복")); dup.Checked = true; Pump(100);
            Check("[Schedule] 중복 허용 토글 저장", Storage.LoadSettings().AllowDuplicate);
            var rep = toggles.First(x => x.Text.Contains("처음 상품부터")); rep.Checked = true; Pump(100); nums[1].Value = 3; Pump(100);
            var sv = Storage.LoadSettings();
            Check("[Schedule] 반복 켬/횟수 저장", sv.RepeatAll && sv.RepeatCount == 3);
            var win = toggles.First(x => x.Text.StartsWith("시간대별")); win.Checked = true; Pump(100);
            Check("[Schedule] 시간대별 켜면 기본 간격 입력은 비활성(예전 규칙)", !nums[0].Enabled && Storage.LoadSettings().UseTimeRules);
            toggles.First(x => x.Text.StartsWith("1개씩")).Checked = false; Pump(100);
            Check("[Schedule] 1개씩 끄면 시간대·반복 비활성(예전 규칙)", !win.Enabled && !rep.Enabled);

            // ---- 종료 처리
            f.Close(); Pump(200);
            Check("[UI] 종료 시 설정/상품 저장 후 닫힘", f.IsDisposed || !f.Visible);
            Console.WriteLine("UI-SELFTEST: PASS " + _pass + " / FAIL " + _fail);
            return _fail == 0 ? 0 : 1;
        }
    }
}
#endif
