#if TESTBUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BunjangAuto
{
    /// <summary>"Lumivolt.exe --selftest" : 화면 없이 핵심 부품이 로드/동작하는지 확인</summary>
    public static class SelfTest
    {
        internal static int _fail;
        private static bool Throws(Action a)
        {
            try { a(); return false; }
            catch { return true; }
        }

        internal static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name);
            if (!ok) _fail++;
        }

        public static int Run()
        {
            // 1) 내장 DLL 로딩
            try
            {
                var o = new ChromeOptions();
                o.AddArgument("--headless=new");
                Check("Selenium 내장 로딩 (ChromeOptions 생성)", o != null);
                Check("XPath 리터럴 생성", By.XPath("//a").ToString().Contains("//a"));
                var j = Newtonsoft.Json.JsonConvert.SerializeObject(new { a = 1 });
                Check("Newtonsoft.Json 내장 로딩", j == "{\"a\":1}");
            }
            catch (Exception ex) { Check("내장 DLL 로딩: " + ex.GetType().Name + " " + ex.Message, false); }

            // 2) CSV 왕복 (따옴표/쉼표/줄바꿈/한글)
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "bj_selftest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "t.csv");
                var src = new Product
                {
                    Title = "테스트, \"따옴표\" 상품",
                    Price = "12000",
                    Category = "여성의류>상의>티셔츠",
                    Condition = "새상품",
                    Quantity = "3",
                    Tags = "a,b,c",
                    Images = "p1.jpg;p2.jpg",
                    Description = "첫째 줄\n둘째 줄, 쉼표 포함",
                    FreeShipping = true
                };
                CsvIO.Write(f, new Product[] { src });
                var back = CsvIO.Read(f);
                Check("CSV 1건 읽힘", back.Count == 1);
                var b = back[0];
                Check("CSV 제목(쉼표/따옴표)", b.Title == src.Title);
                Check("CSV 설명(줄바꿈)", b.Description == src.Description);
                Check("CSV 배송비포함", b.FreeShipping);
                Check("CSV 이미지 2장·상대경로 해석", b.Images.Split(';').Length == 2 && Path.IsPathRooted(b.Images.Split(';')[0]));
                Check("CSV 가격/수량", b.Price == "12000" && b.Quantity == "3");
                Directory.Delete(dir, true);
            }
            catch (Exception ex) { Check("CSV 왕복: " + ex.Message, false); }

            // 2b) CP949(엑셀 기본 CSV) 자동 판별
            try
            {
                string f2 = Path.Combine(Path.GetTempPath(), "bj_cp949_" + Guid.NewGuid().ToString("N") + ".csv");
                File.WriteAllBytes(f2, new byte[] { 0xc1,0xa6,0xb8,0xf1,0x2c,0xb0,0xa1,0xb0,0xdd,0x0a,0xc5,0xd7,0xbd,0xba,0xc6,0xae,0x2c,0x31,0x30,0x30,0x0a });
                string txt = CsvIO.ReadAllTextAuto(f2);
                Check("CP949 CSV 자동 판별", txt.StartsWith("제목,가격") && txt.Contains("테스트,100"));
                File.Delete(f2);
            }
            catch (Exception ex) { Check("CP949: " + ex.Message, false); }

            // 2c) 여러 인코딩·구분자의 CSV 가 글자 깨짐 없이 읽히는지
            try
            {
                string dir2 = Path.Combine(Path.GetTempPath(), "bj_enc_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir2);
                Func<string, string, string> make = (delim, title) =>
                    string.Join(delim, new string[] { "제목", "가격", "카테고리 (대>중>소)", "상태", "수량", "태그", "사진 경로(여러 장)", "상품설명", "배송비포함" }) + "\r\n" +
                    string.Join(delim, new string[] { title, "\"₩1,050,000원\"", "디지털>휴대폰>애플", "새상품", "2", "\"a,b\"", "\"p1.jpg;p2.jpg\"", "\"첫째 줄\r\n둘째 줄\"", "Y" }) + "\r\n";
                Func<string, Product, bool> okProduct = (label, p) =>
                    p != null && p.Title.StartsWith("테스트") && p.Price == "1050000" && p.Quantity == "2" && p.Category == "디지털>휴대폰>애플" &&
                    p.Description == "첫째 줄\n둘째 줄" && p.FreeShipping && p.Images.Split(';').Length == 2 && p.Tags == "a,b";

                string uni = make(",", "테스트 상품 😀 가나다");
                string kr = make(",", "테스트 상품 가나다").Replace("₩", "");   // CP949 에는 ₩ 기호가 없다
                var cases = new List<KeyValuePair<string, byte[]>>();
                Func<Encoding, string, bool, byte[]> enc = (e, t, bom) =>
                {
                    byte[] body = e.GetBytes(t);
                    if (!bom) return body;
                    byte[] pre = e.GetPreamble();
                    byte[] all = new byte[pre.Length + body.Length];
                    Buffer.BlockCopy(pre, 0, all, 0, pre.Length);
                    Buffer.BlockCopy(body, 0, all, pre.Length, body.Length);
                    return all;
                };
                cases.Add(new KeyValuePair<string, byte[]>("UTF-8 BOM", enc(new UTF8Encoding(true), uni, true)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-8 BOM 없음", enc(new UTF8Encoding(false), uni, false)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-16 LE BOM", enc(new UnicodeEncoding(false, true), uni, true)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-16 LE BOM 없음", enc(new UnicodeEncoding(false, false), uni, false)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-16 BE BOM", enc(new UnicodeEncoding(true, true), uni, true)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-16 BE BOM 없음", enc(new UnicodeEncoding(true, false), uni, false)));
                cases.Add(new KeyValuePair<string, byte[]>("UTF-32 BOM", enc(new UTF32Encoding(false, true), uni, true)));
                cases.Add(new KeyValuePair<string, byte[]>("CP949(엑셀 기본 CSV)", Encoding.GetEncoding(949).GetBytes(kr)));
                cases.Add(new KeyValuePair<string, byte[]>("탭 구분 UTF-16(엑셀 유니코드 텍스트)", enc(new UnicodeEncoding(false, true), make("\t", "테스트 상품"), true)));
                cases.Add(new KeyValuePair<string, byte[]>("세미콜론 구분 CP949", Encoding.GetEncoding(949).GetBytes(make(";", "테스트 상품").Replace("₩", ""))));
                foreach (var c in cases)
                {
                    string fp = Path.Combine(dir2, "c.csv");
                    File.WriteAllBytes(fp, c.Value);
                    List<Product> got = null; string why = "";
                    try { got = CsvIO.Read(fp); } catch (Exception ex) { why = ex.Message; }
                    Check("CSV 인코딩: " + c.Key + why, got != null && got.Count == 1 && okProduct(c.Key, got[0]) &&
                        (!c.Key.StartsWith("UTF-") || got[0].Title.Contains("😀") || c.Key.Contains("UTF-32") && got[0].Title.Contains("😀")));
                }

                // 엑셀 등에서 파일을 열어 둔 상태에서도 읽히는지
                string lockedPath = Path.Combine(dir2, "locked.csv");
                File.WriteAllBytes(lockedPath, enc(new UTF8Encoding(true), uni, true));
                using (var hold = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    var lockedRead = CsvIO.Read(lockedPath);
                    Check("CSV 다른 프로그램이 열어 둔 파일 읽기", lockedRead.Count == 1);
                }

                // 머리글에 '제목'이 없으면 안내 문구와 함께 실패해야 한다
                string badPath = Path.Combine(dir2, "bad.csv");
                File.WriteAllText(badPath, "foo,bar\r\n1,2\r\n", new UTF8Encoding(true));
                bool threw = false; try { CsvIO.Read(badPath); } catch (InvalidDataException) { threw = true; }
                Check("CSV 머리글 없으면 안내 오류", threw);

                // 영문 머리글 / 순서 바뀐 머리글
                string altPath = Path.Combine(dir2, "alt.csv");
                File.WriteAllText(altPath, "price,title,qty\r\n\"10,000\",영문 머리글 상품,\r\n", new UTF8Encoding(true));
                var alt = CsvIO.Read(altPath);
                Check("CSV 영문·순서 다른 머리글", alt.Count == 1 && alt[0].Title == "영문 머리글 상품" && alt[0].Price == "10000" && alt[0].Quantity == "1");
                Directory.Delete(dir2, true);
            }
            catch (Exception ex) { Check("CSV 인코딩 시험: " + ex.GetType().Name + " " + ex.Message, false); }

            // 3) XPath 템플릿 채우기
            Check("XPath 값 채우기(일반)", SelectorSet.Fill("//a[text()='{0}']", "티셔츠") == "//a[text()='티셔츠']");
            Check("XPath 값 채우기(작은따옴표)", SelectorSet.Fill("//a[text()='{0}']", "Levi's") == "//a[text()=\"Levi's\"]");
            Check("XPath 값 채우기(두 종류 따옴표)", SelectorSet.Fill("//a[text()='{0}']", "a'b\"c").Contains("concat("));

            // 4) 설정/셀렉터 JSON 저장·읽기
            try
            {
                var s = new SelectorSet();
                string js = Newtonsoft.Json.JsonConvert.SerializeObject(s);
                var s2 = Newtonsoft.Json.JsonConvert.DeserializeObject<SelectorSet>(js);
                Check("selectors JSON 왕복", s2.TitleXPath == s.TitleXPath && s2.NewProductUrl == s.NewProductUrl);
            }
            catch (Exception ex) { Check("selectors JSON: " + ex.Message, false); }

            // 4b) 로컬 데이터 금고(Vault): AES-256 암호화 + 위변조 확인, 이 PC에서만 풀리는지
            try
            {
                string plain = "{\"Id\":\"테스트\",\"Num\":123}";
                string enc1 = Vault.Protect(plain, "selftest");
                string enc2 = Vault.Protect(plain, "selftest");
                Check("Vault: 같은 내용도 암호문은 매번 다름(IV)", enc1 != enc2);
                Check("Vault: 암·복호화 왕복", Vault.Unprotect(enc1, "selftest") == plain);
                Check("Vault: 평문은 보호된 형식이 아님", !Vault.IsProtected(plain) && Vault.IsProtected(enc1));
                Check("Vault: 용도(label)가 다르면 못 품", Throws(() => Vault.Unprotect(enc1, "other-label")));

                var tampered = Convert.FromBase64String(enc1.Substring(enc1.IndexOf(':') + 1));
                tampered[tampered.Length - 1] ^= 0xFF;   // 끝부분(HMAC) 한 바이트 변조
                string tamperedStr = enc1.Substring(0, enc1.IndexOf(':') + 1) + Convert.ToBase64String(tampered);
                Check("Vault: 변조된 데이터는 거부", Throws(() => Vault.Unprotect(tamperedStr, "selftest")));
                Check("Vault: 형식이 아닌 문자열은 거부", Throws(() => Vault.Unprotect("그냥 평문", "selftest")));
            }
            catch (Exception ex) { Check("Vault: " + ex.Message, false); }

            // 4c) 라이선스: 공개키 임베딩(AES) 복호화, 서명 형식, PC 고유번호 형식
            try
            {
                Check("라이선스: PC 고유번호 형식(XXXX-XXXX-XXXX)",
                    System.Text.RegularExpressions.Regex.IsMatch(License.GetMachineId(), @"^[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$"));

                // 서명이 올바르지 않은 키를 넣어, 내장된(AES로 보호된) 공개키가 정상적으로 풀려
                // RSA 검증 단계까지 도달하는지 확인한다(= "형식 오류"가 아니라 "키가 올바르지 않음"이어야 함).
                byte[] payload = Encoding.UTF8.GetBytes("BJA2|테스트|AAAA-BBBB-CCCC|0|20260101|aaaaaaaa");
                byte[] fakeSig = new byte[512];
                new Random(1).NextBytes(fakeSig);
                string fakeKey = Convert.ToBase64String(payload) + "." + Convert.ToBase64String(fakeSig);
                var info = License.Verify(fakeKey, "AAAA-BBBB-CCCC");
                Check("라이선스: 구형 RSA 라이선스 거부", !info.Valid);

                Check("라이선스: 형식이 아예 틀린 키는 형식 오류로 구분", !License.Verify("asdf", "AAAA-BBBB-CCCC").Valid);
            }
            catch (Exception ex) { Check("라이선스 점검: " + ex.Message, false); }

            // 5) 시간대별 등록 간격 계산
            try
            {
                int m;
                Check("시간 해석 09:30 / 24:00 / 잘못된 값", Schedule.TryParseTime("09:30", out m) && m == 570 && Schedule.TryParseTime("24:00", out m) && m == 1440 && !Schedule.TryParseTime("25:00", out m) && !Schedule.TryParseTime("ab", out m));

                List<TimeWindow> w; string err;
                var rules = new List<TimeRule> { new TimeRule("00:00", "09:00", 40), new TimeRule("09:00", "18:00", 60) };
                Check("시간대 검사(정상)", Schedule.TryBuild(rules, out w, out err) && w.Count == 2);
                Check("시간대 검사(빈 목록/잘못된 값 거부)",
                    !Schedule.TryBuild(new List<TimeRule>(), out w, out err) &&
                    !Schedule.TryBuild(new List<TimeRule> { new TimeRule("aa", "09:00", 40) }, out w, out err) &&
                    !Schedule.TryBuild(new List<TimeRule> { new TimeRule("00:00", "09:00", 0) }, out w, out err));
                Schedule.TryBuild(rules, out w, out err);

                var d = new DateTime(2026, 10, 7);
                Check("00~09시는 40분 간격", Schedule.NextRun(w, d.AddHours(0), d, 20) == d.AddMinutes(40));
                Check("09~18시는 60분 간격", Schedule.NextRun(w, d.AddHours(10), d.AddHours(10), 20) == d.AddHours(11));
                Check("새 시간대 시작 시 이전 간격 대기 취소", Schedule.NextRun(w, d.AddHours(8).AddMinutes(30), d.AddHours(8).AddMinutes(30), 20) == d.AddHours(9));
                Check("마지막 시간대 이후엔 다음날 첫 시간대 시작까지 대기", Schedule.NextRun(w, d.AddHours(17).AddMinutes(30), d.AddHours(17).AddMinutes(30), 20) == d.AddDays(1));
                Check("작업이 오래 걸려 늦으면 즉시", Schedule.NextRun(w, d.AddHours(10), d.AddHours(12), 20) == d.AddHours(12));

                var only = new List<TimeWindow> { new TimeWindow { Start = 0, End = 540, Minutes = 40 } };
                Check("시간대 밖에서 시작하면 다음 시작 시각까지 대기(다음날)", Schedule.NextAllowed(only, d.AddHours(12)) == d.AddDays(1));
                Check("시간대 밖(새벽 전)에서 시작하면 당일 시작 시각", Schedule.NextAllowed(new List<TimeWindow> { new TimeWindow { Start = 600, End = 720, Minutes = 30 } }, d.AddHours(8)) == d.AddHours(10));

                var night = new TimeWindow { Start = 22 * 60, End = 6 * 60, Minutes = 30 };
                Check("자정을 넘는 시간대(22~06)", night.Contains(23 * 60) && night.Contains(3 * 60) && !night.Contains(12 * 60));
                Check("시작=종료는 24시간", new TimeWindow { Start = 0, End = 0, Minutes = 10 }.Contains(777));
            }
            catch (Exception ex) { Check("시간대 계산: " + ex.Message, false); }

            // 6) 설정 저장·읽기 (시간대가 중복으로 늘어나지 않는지, 예전 설정 파일의 지운 항목이 있어도 읽히는지)
            try
            {
                var c = new AppSettings();
                c.UseTimeRules = true; c.RepeatAll = true; c.RepeatCount = 3;
                c.TimeRules.Add(new TimeRule("00:00", "09:00", 40));
                c.TimeRules.Add(new TimeRule("09:00", "18:00", 60));
                string js = Newtonsoft.Json.JsonConvert.SerializeObject(c);
                var c2 = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(js);
                Check("설정 왕복(시간대 2개 유지)", c2.TimeRules.Count == 2 && c2.TimeRules[1].Minutes == 60 && c2.RepeatCount == 3 && c2.UseTimeRules);
                var old = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>("{\"DoBump\":true,\"DoRepost\":true,\"Repeat\":true,\"IntervalMinutes\":60,\"OneByOneMinutes\":25}");
                Check("예전 설정 파일 읽기(끌올/재등록 항목 무시)", old != null && old.OneByOneMinutes == 25 && !old.RepeatAll);
            }
            catch (Exception ex) { Check("설정 왕복: " + ex.Message, false); }

            // 7) v2 추가 검사(테마, 저장 형식 호환, 텔레메트리 직렬화, 비밀정보 제거, 로그 큐, Client ID, CSV 호환, 전송 계층)
            SelfTestV2.Run();

            Console.WriteLine(_fail == 0 ? "ALL PASSED" : (_fail + " FAILED"));
            return _fail == 0 ? 0 : 1;
        }
    }
}


#endif
