using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    /// <summary>
    /// v2 에서 추가된 부분의 자가 진단. 기존 SelfTest(1~6)는 그대로 두고 그 뒤에 이어서 실행된다.
    /// 사용자의 실제 설정/상품 파일은 건드리지 않는다(저장 형식은 메모리와 임시 파일로만 검사).
    /// </summary>
    public static class SelfTestV2
    {
        private static void Check(string name, bool ok) { SelfTest.Check(name, ok); }

        private static double Lum(System.Drawing.Color c)
        {
            Func<int, double> f = v => { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); };
            return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
        }
        private static double Contrast(System.Drawing.Color a, System.Drawing.Color b) { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

        public static void Run()
        {
            ThemeTest(); FontTest(); HelpTest(); PauseTest(); StorageMigrationTest(); TelemetrySerializationTest(); RedactionTest(); LogQueueTest(); ClientIdTest(); CsvCompatTest(); TransportTest();
        }

        // ---------------------------------------------------------------- Theme
        private static void ThemeTest()
        {
            try
            {
                Check("[Theme] Light/Dark/System 문자열 변환", ThemeService.Parse("Light") == ThemeMode.Light && ThemeService.Parse("dark") == ThemeMode.Dark &&
                      ThemeService.Parse("System") == ThemeMode.System && ThemeService.Parse(null) == ThemeMode.System && ThemeService.Parse("???") == ThemeMode.System);
                var L = ThemePalette.Light; var D = ThemePalette.DarkPal;
                Check("[Theme] 두 팔레트의 밝기가 확실히 다름", Lum(L.Bg) > 0.6 && Lum(D.Bg) < 0.05 && !L.Dark && D.Dark);
                Check("[Theme] 본문 글자 대비 4.5:1 이상(라이트/다크)", Contrast(L.Text, L.Bg) >= 4.5 && Contrast(L.Text, L.Card) >= 4.5 && Contrast(D.Text, D.Bg) >= 4.5 && Contrast(D.Text, D.Card) >= 4.5);
                Check("[Theme] 보조 글자·강조색 대비 4.5:1 이상", Contrast(L.TextMuted, L.Card) >= 4.5 && Contrast(D.TextMuted, D.Card) >= 4.5 && Contrast(L.OnAccent, L.Accent) >= 4.5 && Contrast(D.OnAccent, D.Accent) >= 4.5);
                Check("[Theme] 다크 모드가 검정 한 색이 아닌 계층(사이드바<바탕<카드<카드Alt)", Lum(D.Sidebar) < Lum(D.Bg) && Lum(D.Bg) < Lum(D.Card) && Lum(D.Card) < Lum(D.CardAlt));
                Check("[Theme] 라이트 모드 계층(바탕<카드)", Lum(L.Bg) < Lum(L.Card));
                Check("[Theme] 예전 이름(BgDark/Panel/Gold…)이 현재 팔레트를 가리킴", Theme.BgDark == Theme.Current.Bg && Theme.Panel == Theme.Current.Card && Theme.Gold == Theme.Current.Accent && Theme.TextPrimary == Theme.Current.Text);
                var s = new AppSettings();
                string js = JsonConvert.SerializeObject(s);
                var back = JsonConvert.DeserializeObject<AppSettings>(js.Replace("\"ThemeMode\":\"System\"", "\"ThemeMode\":\"Dark\""));
                var old = JsonConvert.DeserializeObject<AppSettings>("{\"OneByOneMinutes\":25}");
                Check("[Theme] 선택값 저장/복원 + 예전 settings.json 은 System 으로 시작", back.ThemeMode == "Dark" && old.ThemeMode == "System");
            }
            catch (Exception ex) { Check("[Theme] 예외: " + ex.Message, false); }
        }


        // ---------------------------------------------------------------- Font
        private static void FontTest()
        {
            try
            {
                using (var st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("font.NanumGothicBold.ttf"))
                {
                    byte[] head = new byte[4]; int n = st == null ? 0 : st.Read(head, 0, 4);
                    Check("[Font] 나눔고딕 Bold TTF 가 exe 안에 내장됨(TrueType 시그니처, 1MB 이상)", st != null && n == 4 && head[0] == 0 && head[1] == 1 && head[2] == 0 && head[3] == 0 && st.Length > 1000000);
                }
                Check("[Font] 나눔고딕 Bold 를 실제로 쓸 수 있음(" + (AppFont.Active ? "적용" : "대체: " + AppFont.Status) + ")", AppFont.Active);
                using (var f = Theme.Font(9f, System.Drawing.FontStyle.Regular))
                using (var m = Theme.FontMono(9f))
                    Check("[Font] 모든 글꼴 요청이 나눔고딕 Bold 로 만들어짐", AppFont.Active && (f.Name == "NanumGothic" || f.Name == "나눔고딕") && f.Bold && (m.Name == "NanumGothic" || m.Name == "나눔고딕") && m.Bold);
            }
            catch (Exception ex) { Check("[Font] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- 사용 설명서
        private static void HelpTest()
        {
            try
            {
                var topics = HelpService.Topics();
                string[] must = { "프로그램 시작하기", "라이선스 등록", "로그인", "상품 추가", "상품을 앱에서 관리하기", "상품 데이터 작성법", "이미지 등록", "자동 등록", "1개씩 등록", "반복 등록", "시간대별 등록", "예약 기능", "로그 확인", "오류 해결", "Chrome 문제 해결", "로그인 문제 해결", "라이선스 문제 해결", "Light / Dark Mode", "설정", "FAQ" };
                Check("[Help] 요청한 20개 목차가 모두 내장되어 있음", topics.Count == 20 && must.All(m => topics.Any(t => t.Title == m)));
                Check("[Help] 모든 문서가 비어 있지 않고 블록으로 변환됨", topics.All(t => t.Markdown.Length > 150 && HelpService.Build(t).Count >= 3));
                Check("[Help] 검색(제목/본문, 대소문자 무시)", HelpService.Search("상품").Count >= 2 && HelpService.Search("csv").Count == 0 && HelpService.Search("").Count == 20 && HelpService.Search("zzzz-없음").Count == 0 && HelpService.Search("상품을 앱에서 관리하기")[0].Title == "상품을 앱에서 관리하기");
                DocView dv;
                try { dv = new DocView { Width = 760, Height = 400 }; }
                catch (TypeInitializationException) { Console.WriteLine("SKIP  [Help] 본문 뷰어 검사: 화면(디스플레이)이 없는 환경"); return; }
                var blocks = HelpService.Build(topics.First(t => t.Title == "상품을 앱에서 관리하기"));
                dv.SetContent(blocks, HelpService.PlainText(topics[0]), "");
                int wide = dv.ContentHeight; dv.Width = 380; int narrow = dv.ContentHeight;
                Check("[Help] 본문 뷰어: 폭이 좁아지면 줄바꿈으로 높이가 늘어남", wide > 200 && narrow > wide);
                Check("[Help] 본문 뷰어: 평문 복사 텍스트 생성", HelpService.PlainText(topics[0]).Contains("프로그램 시작하기"));
            }
            catch (Exception ex) { Check("[Help] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- 일시정지 (신규 기능)
        private static void PauseTest()
        {
            try
            {
                var bot = new Bot(new AppSettings(), SelectorSet.Load());
                var m = typeof(Bot).GetMethod("CheckStop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Exception err = null; var done = new System.Threading.ManualResetEvent(false);
                System.Threading.ThreadStart body = delegate { try { m.Invoke(bot, null); } catch (System.Reflection.TargetInvocationException e) { err = e.InnerException; } done.Set(); };

                bot.PauseRequested = false;
                var t0 = new System.Threading.Thread(body); t0.Start();
                Check("[Pause] 일시정지가 아니면 단계 점검이 바로 통과", done.WaitOne(1000) && err == null);

                done.Reset(); bot.PauseRequested = true;
                var t1 = new System.Threading.Thread(body); t1.Start();
                bool blocked = !done.WaitOne(700);
                bot.PauseRequested = false;
                bool released = done.WaitOne(2000) && err == null;
                Check("[Pause] 일시정지 중에는 멈춰 있다가, 해제하면 이어서 진행", blocked && released);

                done.Reset(); err = null; bot.PauseRequested = true;
                var t2 = new System.Threading.Thread(body); t2.Start();
                System.Threading.Thread.Sleep(300); bot.StopRequested = true;
                bool ended = done.WaitOne(2000);
                Check("[Pause] 일시정지 중에 중지하면 바로 중지 처리됨", ended && err != null && err.GetType().Name == "StopRequestedException");
                bot.PauseRequested = false; bot.StopRequested = false;
            }
            catch (Exception ex) { Check("[Pause] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- Storage Migration (형식 수준)
        private static void StorageMigrationTest()
        {
            try
            {
                // 예전(평문) settings.json → 새 AppSettings 로 읽혀야 하고, 암호화 형식으로 올려 써도 같은 값이 나와야 한다
                string legacy = "{\"DoRegister\":true,\"RepeatAll\":true,\"RepeatCount\":4,\"OneByOne\":true,\"OneByOneMinutes\":33,\"UseTimeRules\":true," +
                                "\"TimeRules\":[{\"Start\":\"01:00\",\"End\":\"05:00\",\"Minutes\":15}],\"AllowDuplicate\":true,\"LastCsv\":\"C:\\\\a.csv\",\"ChromePath\":\"C:\\\\c\\\\chrome.exe\",\"AssumeLoggedIn\":true}";
                Check("[Migration] 평문 settings.json 은 암호화 대상으로 인식", !Vault.IsProtected(legacy));
                var s = JsonConvert.DeserializeObject<AppSettings>(legacy);
                Check("[Migration] 예전 설정의 모든 값 보존(반복/간격/시간대/중복/크롬경로/로그인)", s.RepeatAll && s.RepeatCount == 4 && s.OneByOneMinutes == 33 && s.UseTimeRules && s.TimeRules.Count == 1 &&
                      s.TimeRules[0].Minutes == 15 && s.AllowDuplicate && s.LastCsv == "C:\\a.csv" && s.ChromePath == "C:\\c\\chrome.exe" && s.AssumeLoggedIn && s.ThemeMode == "System");
                string prot = Vault.Protect(JsonConvert.SerializeObject(s), "settings");
                var s2 = JsonConvert.DeserializeObject<AppSettings>(Vault.Unprotect(prot, "settings"));
                Check("[Migration] 설정 암호화 왕복", Vault.IsProtected(prot) && s2.OneByOneMinutes == 33 && s2.TimeRules[0].End == "05:00");

                // 상품 12개 필드 전부
                var p = new Product { Title = "t,\"q\"\n줄", Price = "1000", Category = "a>b>c", Condition = "사용감 적음", Quantity = "3", Tags = "x,y", Images = "a.jpg;b.png", Description = "설명😀", FreeShipping = true, Status = "등록됨", ProductId = "123", LastPosted = "2026-10-01 10:00:00" };
                string pj = JsonConvert.SerializeObject(new List<Product> { p });
                var lst = JsonConvert.DeserializeObject<List<Product>>(Vault.Unprotect(Vault.Protect(pj, "products"), "products"));
                var q = lst[0];
                Check("[Migration] 상품 12개 필드 전부 보존(암호화 왕복)", q.Title == p.Title && q.Price == "1000" && q.Category == p.Category && q.Condition == p.Condition && q.Quantity == "3" && q.Tags == "x,y" &&
                      q.Images == p.Images && q.Description == p.Description && q.FreeShipping && q.Status == "등록됨" && q.ProductId == "123" && q.LastPosted == p.LastPosted);
                Check("[Migration] 다른 용도(label) 키로는 열리지 않음", Throws(() => Vault.Unprotect(prot, "products")));
            }
            catch (Exception ex) { Check("[Migration] 예외: " + ex.Message, false); }
        }
        private static bool Throws(Action a) { try { a(); return false; } catch { return true; } }

        // ---------------------------------------------------------------- Telemetry serialization
        private static void TelemetrySerializationTest()
        {
            try
            {
                var e = new LogEntry { Level = "ERROR", Module = "ProductUpload", Action = "image.upload.fail", Message = "이미지 업로드 실패 😀", Result = "FAIL", ExcType = "System.IO.IOException", Version = "2.0.0.0", Session = "abc" };
                var j = JObject.Parse(JsonConvert.SerializeObject(e), new JsonLoadSettings());
                var jr = new JsonTextReader(new StringReader(JsonConvert.SerializeObject(e))) { DateParseHandling = DateParseHandling.None }; j = JObject.Load(jr);
                string[] keys = { "id", "ts", "level", "module", "action", "message", "result", "exc_type", "version", "session" };
                Check("[Telemetry] 이벤트 JSON 필드(서버 스키마와 일치)", keys.All(k => j[k] != null) && j.Properties().Count() == keys.Length);
                Check("[Telemetry] ts 는 UTC ISO-8601(Z)", System.Text.RegularExpressions.Regex.IsMatch((string)j["ts"], @"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$"));
                var back = JsonConvert.DeserializeObject<LogEntry>(j.ToString());
                Check("[Telemetry] 직렬화 왕복(한글/이모지/예외형)", back.Message == e.Message && back.ExcType == e.ExcType && back.Id == e.Id);
                Check("[Telemetry] 이벤트 ID 고유", new LogEntry().Id != new LogEntry().Id);

                var c1 = LogClassifier.Classify("  → 등록 완료");
                var c2 = LogClassifier.Classify("  → 실패: 이미지 업로드에 실패했습니다");
                var c3 = LogClassifier.Classify("예외: 값이 없습니다 System.NullReferenceException");
                var c4 = LogClassifier.Classify("[등록] 아이폰 (1/3)");
                var c5 = LogClassifier.Classify("CSV 5건을 불러왔습니다: C:\\a.csv");
                var c6 = LogClassifier.Classify("전혀 모르는 평범한 문구");
                Check("[Telemetry] 기존 로그 문구 분류(성공/실패/예외/시작/CSV/기타)",
                      c1.Action == "product.register.success" && c1.Result == "OK" && c2.Action == "image.upload.fail" && c2.Level == "ERROR" &&
                      c3.Action == "exception" && c3.ExcType == "System.NullReferenceException" && c4.Action == "product.register.start" && c5.Action == "csv.import" && c6.Level == "INFO" && c6.Action == "log");
                var cfg = new TelemetryConfig { ServerUrl = "http://evil.example.com", IngestKey = "k" };
                var cfg2 = new TelemetryConfig { ServerUrl = "https://logs.example.com", IngestKey = "k" };
                var cfg3 = new TelemetryConfig { ServerUrl = "http://127.0.0.1:8080", IngestKey = "k" };
                Check("[Telemetry] 서버 주소는 https(또는 루프백 개발용)만 허용", !cfg.UrlAllowed && cfg2.UrlAllowed && cfg3.UrlAllowed && cfg2.Active && !new TelemetryConfig { ServerUrl = "https://x", IngestKey = "" }.Active);
            }
            catch (Exception ex) { Check("[Telemetry] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- Redaction
        private static void RedactionTest()
        {
            try
            {
                Func<string, string> S = Redactor.Scrub;
                Check("[Redaction] password=값", !S("로그인 password=Abc123!@# 시도").Contains("Abc123") && S("로그인 password=Abc123").Contains("password="));
                Check("[Redaction] 비밀번호: 값(한글 키)", !S("비밀번호: hunter22").Contains("hunter22"));
                Check("[Redaction] Authorization/Bearer", !S("Authorization: Bearer abcdefghijklmnop12345").Contains("abcdefghij") && !S("헤더 Bearer abcdefghijklmnop12345 전송").Contains("abcdefghij"));
                Check("[Redaction] Cookie/Set-Cookie 헤더", !S("Cookie: sid=abc123; token=zzz999\r\n다음줄").Contains("abc123") && S("Cookie: a=b\r\n다음줄").Contains("다음줄"));
                Check("[Redaction] 세션/토큰 key=value", !S("session=9f8e7d6c5b4a&x=1").Contains("9f8e7d6c") && !S("access_token=ya29.AbCdEf").Contains("ya29") && !S("{\"refresh_token\":\"r-123456\"}").Contains("r-123456"));
                Check("[Redaction] JWT", !S("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk 끝").Contains("eyJhbGci"));
                Check("[Redaction] 개인키 블록", !S("-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA\n-----END RSA PRIVATE KEY-----").Contains("MIIEow") && !S("<RSAKeyValue><Modulus>abc</Modulus><D>QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo=</D></RSAKeyValue>").Contains("QUJDREVG"));
                Check("[Redaction] 카드번호(Luhn 통과)만 가림", !S("카드 4111 1111 1111 1111 결제").Contains("4111") && S("상품번호 1234567890123 확인").Contains("1234567890123"));
                Check("[Redaction] 계좌번호 형태(110-123-456789)", !S("계좌 110-123-456789 입금").Contains("456789"));
                Check("[Redaction] Windows 사용자 폴더명", !S("C:\\Users\\홍길동\\AppData\\Roaming\\BunjangAuto").Contains("홍길동") && S("C:\\Users\\홍길동\\AppData").Contains("AppData"));
                Check("[Redaction] 이메일", !S("계정 hong@example.com 로그인").Contains("hong@example.com"));
                string[] ok = { "[등록] 아이폰 15 프로 256GB (1/30)", "  → 등록 완료", "가격 1050000원, 수량 3", "[1개씩 등록] 다음 등록 예정: 14:20:05", "크롬 데이터 폴더: C:\\ProgramData\\BunjangAuto\\chrome-profile" };
                Check("[Redaction] 일반 운영 로그는 그대로 보존", ok.All(x => S(x) == x));
                string[] real = {
                    "https://m.bunjang.co.kr/products/123456789 등록 확인",
                    "ChromeDriver 다운로드: https://storage.googleapis.com/chrome-for-testing-public/131.0.6778.85/win64/chromedriver-win64.zip",
                    "크롬 데이터 폴더: C:\\ProgramData\\BunjangAuto\\chrome-profile\\Default",
                    "[등록] 나이키 에어포스 270 화이트 새상품 (12/30)",
                    "  → 등록 완료 (상품번호 1234567890)",
                    "화면 캡처: C:\\Users\\<user>\\AppData\\Roaming\\BunjangAuto\\screenshots\\20261009-142005.png" };
                Check("[Redaction] 실제 로그 형태(URL·경로·상품번호·드라이버 주소)는 과하게 지워지지 않음", real.All(x => S(x) == x));
                var en = new LogEntry { Message = "token=abc123 실패", Module = "M", Action = "a" }; Redactor.ScrubEntry(en);
                Check("[Redaction] LogEntry 단위 처리", !en.Message.Contains("abc123") && en.Message.Contains("실패"));
            }
            catch (Exception ex) { Check("[Redaction] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- Log queue
        private static void LogQueueTest()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "bja-q-" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                var q = new LogQueue(tmp, 5000);
                for (int i = 0; i < 6000; i++) q.Enqueue(new LogEntry { Message = "m" + i });
                Check("[LogQueue] 상한 5000건, 넘치면 오래된 것부터 버림", q.Count == 5000 && q.Dropped == 1000);
                var b = q.Take(50);
                Check("[LogQueue] 앞에서부터 묶음(batch) 꺼내기", b.Count == 50 && b[0].Message == "m1000" && q.Count == 4950);
                q.Requeue(b);
                var b2 = q.Take(50);
                Check("[LogQueue] 전송 실패 시 되돌려도 순서 유지", q.Count == 4950 && b2[0].Message == "m1000" && b2[49].Message == "m1049");
                var small = new LogQueue(tmp, 100);
                small.Enqueue(new LogEntry { Message = "password=secret999 로그" }); small.Enqueue(new LogEntry { Message = "둘째" });
                small.Persist();
                string raw = File.ReadAllText(tmp, Encoding.UTF8);
                Check("[LogQueue] 디스크 보관 시에도 비밀정보 제거", !raw.Contains("secret999") && raw.Contains("둘째"));
                var again = new LogQueue(tmp, 100);
                int n = again.Restore();
                Check("[LogQueue] 다음 실행에서 복원(순서 유지)하고 파일 정리", n == 2 && again.Take(5)[1].Message == "둘째" && !File.Exists(tmp));
                // 네트워크가 없어도 예외 없이 쌓이기만 하는지: 빈 서버 주소 + 가짜 전송(항상 실패)
                var cfg = new TelemetryConfig { ServerUrl = "https://offline.invalid", IngestKey = "k", BatchSize = 10 };
                var qq = new LogQueue(null, 100); for (int i = 0; i < 25; i++) qq.Enqueue(new LogEntry { Message = "x" + i });
                var svc = new CentralLogService(cfg, qq, new FakeTransport { Mode = "down" }) { SuppressLocalLog = true };
                bool r = svc.FlushOnce();
                Check("[LogQueue] 서버 불가 시 예외 없이 실패 처리, 기록은 그대로 유지", !r && qq.Count == 25 && !svc.Online);
            }
            catch (Exception ex) { Check("[LogQueue] 예외: " + ex.Message, false); }
            finally { try { File.Delete(tmp); } catch { } }
        }

        // ---------------------------------------------------------------- Client ID
        private static void ClientIdTest()
        {
            try
            {
                string a = ClientIdentityService.ClientId, b = ClientIdentityService.ClientId;
                Guid g;
                Check("[ClientId] 랜덤 UUID 형식이고 같은 설치에서는 유지", Guid.TryParse(a, out g) && a == b && g != Guid.Empty);
                var d = ClientIdentityService.Describe();
                Check("[ClientId] 시스템 정보 항목(버전/OS/CPU/RAM/GPU/해상도/DPI)", new[] { "app_version", "os", "cpu", "ram_mb", "gpu", "screen", "dpi" }.All(k => d.ContainsKey(k)) && ((string)d["app_version"]).Split('.').Length == 4);
                string json = JsonConvert.SerializeObject(d).ToLowerInvariant();
                Check("[ClientId] 시스템 정보에 비밀 항목(password/token/cookie)이 없음", !json.Contains("password") && !json.Contains("cookie") && !json.Contains("token"));
            }
            catch (Exception ex) { Check("[ClientId] 예외: " + ex.Message, false); }
        }

        // ---------------------------------------------------------------- CSV 호환
        private static void CsvCompatTest()
        {
            string f = Path.Combine(Path.GetTempPath(), "bja-csv-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                var p = new Product { Title = "제목, \"따옴표\"", Price = "12000", Category = "디지털>휴대폰>애플", Condition = "사용감 적음", Quantity = "2", Tags = "a,b", Images = "x\\1.jpg;x\\2.png", Description = "첫 줄\r\n둘째 줄 😀 ★", FreeShipping = true };
                CsvIO.Write(f, new[] { p });
                var r = CsvIO.Read(f).Single();
                Check("[CSV] 쓰기→읽기 왕복(쉼표·따옴표·줄바꿈·이모지·특수기호)", r.Title == p.Title && r.Price == "12000" && r.Category == p.Category && r.Condition == p.Condition && r.Quantity == "2" && r.Tags == p.Tags && r.Images.Split(';').Length == 2 && r.Images.Contains("1.jpg") && r.Images.Contains("2.png") &&
                      r.Description.Replace("\r\n", "\n") == p.Description.Replace("\r\n", "\n") && r.FreeShipping);
                File.WriteAllBytes(f, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("제목,가격,배송비포함\r\n옛날 파일 상품,\"15,000원\",Y\r\n")).ToArray());
                var o = CsvIO.Read(f).Single();
                Check("[CSV] 예전 형식 파일(9열 중 일부, 가격 '15,000원', 배송비 Y)도 그대로 열림", o.Title == "옛날 파일 상품" && o.Price == "15000" && o.FreeShipping);
                string tpl = Path.ChangeExtension(f, ".tpl.csv"); CsvIO.WriteTemplate(tpl);
                Check("[CSV] 양식 파일을 다시 읽을 수 있음", CsvIO.Read(tpl).Count > 0); File.Delete(tpl);
            }
            catch (Exception ex) { Check("[CSV] 예외: " + ex.Message, false); }
            finally { try { File.Delete(f); } catch { } }
        }

        // ---------------------------------------------------------------- 전송 계층 / 관리자 API 클라이언트
        public class FakeTransport : ITransport
        {
            public string Mode = "ok"; public List<string> Calls = new List<string>(); public string LastBody = "", RegisterBody = ""; public int IngestStatus = 200; public string Role = "ADMIN";
            public HttpResult Send(string method, string url, string body, Dictionary<string, string> headers, int timeout)
            {
                Calls.Add(method + " " + url);
                if (Mode == "down") return new HttpResult { Status = 0, Error = "NameResolutionFailure" };
                if (url.EndsWith("/api/v1/register")) { RegisterBody = body; return new HttpResult { Status = 200, Body = "{\"token\":\"tok-123\"}" }; }
                
                if (url.EndsWith("/api/v1/ingest")) { LastBody = body; return new HttpResult { Status = IngestStatus, Body = "{\"accepted\":1}" }; }
                if (url.EndsWith("/api/v1/admin/login")) return new HttpResult { Status = 200, Body = "{\"token\":\"adm\",\"role\":\"" + Role + "\"}" };
                return new HttpResult { Status = 404, Body = "{}" };
            }
        }

        private static void TransportTest()
        {
            try
            {
                var cfg = new TelemetryConfig { ServerUrl = "https://logs.test", IngestKey = "k", BatchSize = 50 };
                var q = new LogQueue(null, 100);
                q.Enqueue(new LogEntry { Message = "Authorization: Bearer abcdefghijklmnopqrstu", Action = "log" });
                q.Enqueue(new LogEntry { Message = "정상 로그", Action = "app.start" });
                var ft = new FakeTransport();
                var svc = new CentralLogService(cfg, q, ft) { SuppressLocalLog = true };
                bool ok = svc.FlushOnce();
                Check("[Transport] 등록 → 전송 성공, 대기열 비움", ok && q.Count == 0 && svc.Sent == 2 && svc.Online && ft.Calls.Count == 2);
                Check("[Transport] 서버로 가는 본문에 비밀정보 없음 + 등록 때 시스템 정보 전달", !ft.LastBody.Contains("abcdefghijklmnopqrstu") && ft.LastBody.Contains("정상 로그") && ft.RegisterBody.Contains("app_version") && ft.RegisterBody.Contains("client_id"));
                q.Enqueue(new LogEntry { Message = "하나 더" }); ft.IngestStatus = 500;
                bool bad = svc.FlushOnce();
                Check("[Transport] 서버 오류(500)면 기록을 되돌려 놓고 다음에 재시도", !bad && q.Count == 1);
                var api = new AdminApiClient("https://logs.test", new FakeTransport { Role = "ADMIN" });
                var apiUser = new AdminApiClient("https://logs.test", new FakeTransport { Role = "USER" });
                Check("[Admin] ADMIN 역할만 관리자 센터 열림(USER 는 거부)", api.Login("a", "b") && api.IsAdmin && !apiUser.Login("a", "b") && !apiUser.IsAdmin);
            }
            catch (Exception ex) { Check("[Transport] 예외: " + ex.Message, false); }
        }

        /// <summary>실제 서버에 대한 종단 테스트: BunjangAuto.exe --selftest-central URL KEY</summary>
        public static int RunCentral(string url, string key)
        {
            var cfg = new TelemetryConfig { ServerUrl = url, IngestKey = key, BatchSize = 50 };
            var q = new LogQueue(null, 1000);
            var svc = new CentralLogService(cfg, q, new HttpTransport()) { SuppressLocalLog = true };
            string tag = "e2e-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            q.Enqueue(new LogEntry { Level = "INFO", Module = "App", Action = "app.start", Message = tag + " 시작" });
            q.Enqueue(new LogEntry { Level = "INFO", Module = "ProductUpload", Action = "product.register.start", Message = tag + " [등록] 상품" });
            q.Enqueue(new LogEntry { Level = "INFO", Module = "ProductUpload", Action = "product.register.success", Message = tag + " → 등록 완료", Result = "OK" });
            q.Enqueue(new LogEntry { Level = "ERROR", Module = "ProductUpload", Action = "image.upload.fail", Message = tag + " 이미지 업로드 실패 password=SECRET-VALUE-1", Result = "FAIL", ExcType = "System.IO.IOException" });
            bool ok = svc.FlushOnce();
            Console.WriteLine((ok ? "SENT " : "FAILED ") + tag + " client=" + ClientIdentityService.ClientId + " err=" + svc.LastError);
            return ok ? 0 : 1;
        }
    }
}

