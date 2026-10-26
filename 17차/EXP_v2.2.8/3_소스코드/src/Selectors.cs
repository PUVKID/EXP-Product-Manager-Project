using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>
    /// 사이트 화면 요소를 찾는 주소(URL)와 XPath 모음.
    /// 번개장터 화면이 바뀌면 프로그램을 다시 빌드하지 않고
    /// %AppData%\BunjangAuto\selectors.json 만 고치면 된다.
    /// ({0} 은 프로그램이 채워 넣는 자리 표시자)
    /// </summary>
    public class SelectorSet
    {
        /// <summary>기본값이 바뀌면 올려서, 예전 selectors.json 을 새 기본값으로 교체한다.</summary>
        public const int CurrentVersion = 3;
        public int Version = CurrentVersion;

        // ----- 주소 (진단 파일로 확인한 실제 주소) -----
        public string HomeUrl = "https://m.bunjang.co.kr/";
        public string NewProductUrl = "https://m.bunjang.co.kr/products/new";
        /// <summary>{0} = 페이지 번호(0부터)</summary>
        public string ManageUrl = "https://m.bunjang.co.kr/products/manage?tab=ALL&size=10&page={0}";
        public string ProductUrl = "https://m.bunjang.co.kr/products/{0}";

        // ----- 로그인 -----
        /// <summary>로그인 후에만 보이는 헤더의 '알림' 버튼</summary>
        public string LoggedInXPath = "//button[@aria-label='알림']";
        public string LoginOpenXPath = "//*[self::a or self::button][contains(normalize-space(.),'로그인')]";
        public string LoginIdXPath = "//input[contains(@placeholder,'아이디') or contains(@placeholder,'이메일') or contains(@placeholder,'휴대폰') or @type='email' or @type='tel']";
        public string LoginPwXPath = "//input[@type='password']";
        public string LoginSubmitXPath = "//button[contains(normalize-space(.),'로그인')]";

        // ----- 상품 등록 화면 -----
        public string ImageInputXPath = "//input[@id='media-input']";
        public string TitleXPath = "//input[@name='common.name'][not(contains(@placeholder,'검색')) and not(@type='search')]";
        /// <summary>비워 두면 클릭 없이 바로 항목을 고른다(카테고리 목록이 화면에 이미 펼쳐져 있음)</summary>
        public string CategoryOpenXPath = "";
        public string CategoryItemXPath = "//*[@id='scroll-categoryId']//li[normalize-space(.)='{0}']";
        public string ConditionOpenXPath = "//*[@id='scroll-condition']//button[@aria-haspopup='listbox']";
        public string ConditionOptionXPath = "//*[@role='option' or self::li][contains(translate(normalize-space(.),' ',''),'{0}')]";
        public string DescriptionXPath = "//*[@id='scroll-description']//textarea";
        public string TagXPath = "//input[contains(@placeholder,'태그')]";
        public string PriceXPath = "//*[@id='scroll-price']//input[@inputmode='numeric']";
        public string ShippingOpenXPath = "//button[@aria-haspopup='listbox'][.//span[contains(normalize-space(.),'배송비')]]";
        public string ShippingOptionXPath = "//*[@role='option' or self::li][contains(translate(normalize-space(.),' ',''),'{0}')]";
        public string QuantityXPath = "//*[@id='scroll-quantity']//input";
        public string SubmitXPath = "//button[@type='submit' and normalize-space(.)='등록하기']";
        /// <summary>등록 성공 후 주소에서 상품번호를 뽑는 정규식</summary>
        public string ProductIdRegex = "/products/(\\d+)";

        // ----- 내 상품 관리 화면 (/products/manage) -----
        public string RowXPath = "//*[@role='row'][.//input[@type='checkbox' and not(@id='header-checkbox')]]";
        public string RowLinkXPath = ".//a[contains(@href,'/products/')]";
        public string RowTitleXPath = ".//a[contains(@href,'/products/')]/span";
        public string DialogConfirmXPath = "//button[normalize-space(.)='삭제' or normalize-space(.)='확인' or normalize-space(.)='끌어올리기']";
        public string NextPageXPath = "//button[@aria-label='다음 페이지' and not(@disabled)]";

        // ----- 점검 도구가 확인할 항목(이름 → 어느 화면에서 확인할지) -----
        public static readonly string[] CheckOnNewProduct = new string[]
        {
            "ImageInputXPath", "TitleXPath", "ConditionOpenXPath", "DescriptionXPath",
            "TagXPath", "PriceXPath", "ShippingOpenXPath", "QuantityXPath", "SubmitXPath"
        };

        public static SelectorSet Load()
        {
            try
            {
                if (File.Exists(Paths.Selectors))
                {
                    var s = JsonConvert.DeserializeObject<SelectorSet>(File.ReadAllText(Paths.Selectors, Encoding.UTF8));
                    if (s != null && s.Version >= CurrentVersion) return s;
                    // 예전 기본값으로 만들어진 파일 → 백업하고 새 기본값으로 교체
                    try { File.Copy(Paths.Selectors, Paths.Selectors + ".old", true); } catch { }
                }
            }
            catch { }
            var def = new SelectorSet();
            def.Save();
            return def;
        }

        public void Save()
        {
            Directory.CreateDirectory(Paths.Root);
            File.WriteAllText(Paths.Selectors, JsonConvert.SerializeObject(this, Formatting.Indented), Encoding.UTF8);
        }

        public static string GetField(SelectorSet s, string name)
        {
            FieldInfo f = typeof(SelectorSet).GetField(name);
            return f == null ? "" : (string)f.GetValue(s);
        }

        /// <summary>XPath 문자열 리터럴 안전하게 만들기 (작은따옴표 포함 대응)</summary>
        public static string XLit(string s)
        {
            if (s.IndexOf('\'') < 0) return "'" + s + "'";
            if (s.IndexOf('"') < 0) return "\"" + s + "\"";
            var parts = s.Split('\'');
            var sb = new StringBuilder("concat(");
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(",\"'\",");
                sb.Append("'" + parts[i] + "'");
            }
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>"…normalize-space(text())='{0}'…" 같은 템플릿에 값을 안전하게 끼워 넣는다.</summary>
        public static string Fill(string template, string value)
        {
            // 템플릿 안의 '{0}' (따옴표 포함)를 XPath 리터럴로 교체
            if (template.Contains("'{0}'")) return template.Replace("'{0}'", XLit(value));
            return template.Replace("{0}", value);
        }
    }
}
