using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BunjangAuto
{
    /// <summary>"BunjangAuto.exe --selftest" : 화면 없이 핵심 부품이 로드/동작하는지 확인</summary>
    public static class SelfTest
    {
        private static int _fail;
        private static void Check(string name, bool ok)
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

            Console.WriteLine(_fail == 0 ? "ALL PASSED" : (_fail + " FAILED"));
            return _fail == 0 ? 0 : 1;
        }
    }
}
