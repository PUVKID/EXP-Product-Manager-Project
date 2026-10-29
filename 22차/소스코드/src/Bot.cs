using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BunjangAuto
{
    public class StopRequestedException : Exception { }
    public class RegistrationBlockedException : Exception { public RegistrationBlockedException(string message) : base(message) { } }

    /// <summary>번개장터 화면을 Selenium 으로 조작하는 부분</summary>
    public class Bot : IDisposable
    {
        private IWebDriver _driver;
        private ChromeDriverService _service;
        private readonly AppSettings _cfg;
        private readonly SelectorSet _sel;
        private readonly Random _rnd = new Random();

        /// <summary>true 로 바뀌면 가능한 한 빨리 멈춘다</summary>
        public volatile bool StopRequested;

        /// <summary>true 이면 CheckStop() 지점(모든 대기/단계 사이)에서 멈춰 있다가, false 로 바뀌면 이어서 진행한다. (v2 일시정지)</summary>
        public volatile bool PauseRequested;

        /// <summary>자동 로그인이 안 될 때 사용자에게 "브라우저에서 직접 로그인하세요"를 알리는 콜백</summary>
        public Action<string> NotifyManual;

        /// <summary>사용자에게 "예/아니오"로 묻는 콜백 (UI 스레드에서 메시지 박스를 띄움). 예 = true</summary>
        public Func<string, bool> Ask;

        public Bot(AppSettings cfg, SelectorSet sel)
        {
            _cfg = cfg;
            _sel = sel;
        }

        public bool IsAlive
        {
            get
            {
                if (_driver == null) return false;
                try { var t = _driver.Title; return true; } catch { return false; }
            }
        }

        // =============== 브라우저 시작/종료 ===============
        public void Start()
        {
            if (IsAlive) return;
            Close();
            string chrome = DriverManager.FindChromeExe(_cfg.ChromePath);
            string driverPath = DriverManager.EnsureDriver(chrome, Log.Write);

            string mainProfile = Paths.Profile;
            if (!Paths.IsAscii(Paths.Root))
                Log.Write("사용자 폴더에 한글이 있어 크롬 데이터 폴더를 영문 경로로 사용합니다: " + mainProfile);

            // 1차: 일반 옵션 / 2차: 호환 옵션 / 3차: 새 임시 프로필(로그인 유지 안 됨)
            Exception last = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string profile = attempt < 3 ? mainProfile : mainProfile + "-tmp" + DateTime.Now.ToString("HHmmss");
                try
                {
                    KillStaleChrome(profile);
                    Directory.CreateDirectory(profile);
                    if (attempt == 1) Log.Write("크롬 브라우저를 여는 중...");
                    else if (attempt == 2) Log.Write("크롬이 바로 종료되어 호환 옵션으로 다시 시도합니다...");
                    else Log.Write("새 임시 프로필로 다시 시도합니다(이번 실행에서는 로그인이 유지되지 않습니다)...");
                    LaunchOnce(chrome, driverPath, profile, attempt >= 2);
                    Log.Write("INFO", "Selenium", "selenium.start", "  Selenium(ChromeDriver) 세션을 시작했습니다.", "OK");
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    string first = ex.Message.Split('\n')[0];
                    Log.Write("  크롬 시작 실패(" + attempt + "/3): " + first);
                    try { if (_service != null) _service.Dispose(); } catch { }
                    _driver = null; _service = null;
                    Thread.Sleep(1000);
                }
            }
            throw new Exception("크롬을 시작하지 못했습니다. 열려 있는 모든 크롬 창(작업 관리자의 chrome.exe 포함)을 종료한 뒤 다시 시도하세요. " +
                                "설정에서 자가진단을 실행하거나 "+Brand.Email+"로 문의해 주세요. " + last.Message.Split('\n')[0]);
        }

        private void LaunchOnce(string chrome, string driverPath, string profile, bool compat)
        {
            _service = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(driverPath), Path.GetFileName(driverPath));
            _service.HideCommandPromptWindow = true;
            try
            {
                Directory.CreateDirectory(Paths.Logs);
                _service.LogPath = Path.Combine(Paths.Logs, "chromedriver.log");
                _service.EnableVerboseLogging = true;
            }
            catch { }

            var opt = new ChromeOptions();
            if (!string.IsNullOrEmpty(chrome)) opt.BinaryLocation = chrome;
            opt.AddArgument("--user-data-dir=" + profile);   // 로그인 상태 유지
            opt.AddArgument("--window-size=1280,900");
            opt.AddArgument("--lang=ko-KR");
            opt.AddArgument("--disable-notifications");
            opt.AddArgument("--no-first-run");
            opt.AddArgument("--no-default-browser-check");
            opt.AddArgument("--remote-allow-origins=*");
            if (compat)
            {
                opt.AddArgument("--disable-gpu");
                opt.AddArgument("--disable-extensions");
                opt.AddArgument("--disable-dev-shm-usage");
                opt.AddArgument("--no-sandbox");
            }

            _driver = new ChromeDriver(_service, opt, TimeSpan.FromSeconds(120));
            try { _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60); } catch { }
        }

        /// <summary>이전 실행에서 남은 크롬이 같은 프로필을 잡고 있으면 새 크롬이 바로 종료되므로 정리한다.</summary>
        private static void KillStaleChrome(string profileDir)
        {
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'chrome.exe'"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        string cl = o["CommandLine"] as string;
                        if (cl == null || cl.IndexOf(profileDir, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        try
                        {
                            var pr = System.Diagnostics.Process.GetProcessById(Convert.ToInt32(o["ProcessId"]));
                            pr.Kill();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public void Close()
        {
            if (_driver != null) Log.Write("INFO", "Selenium", "selenium.stop", "  Selenium 세션을 종료합니다.", "OK");
            try { if (_driver != null) _driver.Quit(); } catch { }
            try { if (_service != null) _service.Dispose(); } catch { }
            _driver = null;
            _service = null;
        }

        public void Dispose() { Close(); }

        // =============== 로그인 ===============
        /// <summary>
        /// 아이디/비밀번호는 받지 않는다. 번개장터 로그인 화면으로 이동해 두고 사용자가 열린 크롬 창에서 직접 로그인한다.
        /// </summary>
        public bool EnsureLogin()
        {
            Start();
            Go(_sel.HomeUrl);
            Sleep(1500);
            if (IsLoggedIn()) { Log.Write("이미 로그인되어 있습니다."); return true; }
            if (_cfg.AssumeLoggedIn)
            {
                Log.Write("이전에 로그인을 확인했으므로 로그인된 상태로 보고 진행합니다. (로그인이 풀렸다면 '로그인 상태 초기화'를 누르세요)");
                return true;
            }

            // 번개장터 로그인 화면을 열어 준다
            try
            {
                var open = Find(_sel.LoginOpenXPath, 4);
                if (open != null) { Click(open); Sleep(1000); }
            }
            catch (StopRequestedException) { throw; }
            catch (Exception ex) { Log.Write("로그인 화면을 자동으로 열지 못했습니다. 크롬 창에서 직접 로그인 화면을 열어 주세요. (" + ex.Message + ")"); }

            string msg = "번개장터 로그인 화면이 열려 있는 크롬 창에서 직접 로그인해 주세요.\r\n(전화번호 인증·소셜 로그인도 그 창에서 하시면 됩니다.)\r\n\r\n로그인을 마쳤으면 [예]를 누르세요.";
            Log.Write("열려 있는 크롬 창에서 번개장터에 직접 로그인해 주세요.");
            if (Ask != null)
            {
                if (Ask(msg))
                {
                    bool seen = false;
                    try { seen = WaitLoggedIn(5); } catch (StopRequestedException) { throw; } catch { }
                    _cfg.AssumeLoggedIn = true;
                    try { Storage.SaveSettings(_cfg); } catch { }
                    Log.Write(seen ? "로그인 확인됨. 다음부터는 이 확인을 다시 묻지 않습니다."
                                   : "로그인 완료로 처리합니다. 다음부터는 이 확인을 다시 묻지 않습니다.");
                    return true;
                }
                Log.Write("로그인을 취소했습니다.");
                return false;
            }
            if (NotifyManual != null) NotifyManual(msg);
            if (WaitLoggedIn(180)) { Log.Write("로그인 확인됨"); return true; }
            Log.Write("로그인을 확인하지 못했습니다.");
            Shot("login-failed");
            return false;
        }

        // =============== 진단: 실제 화면 구조 저장 ===============
        /// <summary>
        /// 번개장터 화면의 HTML 과 캡처를 파일로 저장하고 zip 으로 묶는다.
        /// (화면 요소를 찾는 값을 실제 화면에 맞추기 위한 자료)
        /// </summary>
        public string DumpPages()
        {
            Start();
            string dir = Path.Combine(Paths.Root, "diag-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var info = new System.Text.StringBuilder();

            Go(_sel.HomeUrl); Sleep(3000);
            SavePage(dir, "1-home", info);

            string[][] steps = new string[][]
            {
                new string[] { "2-newproduct", "크롬 창에서 '상품 등록'(사진·상품명 입력) 화면을 직접 열어 두고 [예]를 누르세요.\r\n(등록 버튼은 누르지 마세요)" },
                new string[] { "2b-condition-open", "등록 화면에서 '상품상태' 선택칸을 눌러 목록이 펼쳐진 상태로 두고 [예]를 누르세요.\r\n(건너뛰려면 [아니오])" },
                new string[] { "2c-shipping-open", "'택배거래' 배송비 선택칸을 눌러 목록이 펼쳐진 상태로 두고 [예]를 누르세요.\r\n(건너뛰려면 [아니오])" },
                new string[] { "2d-category-sub", "카테고리에서 대분류(예: 디지털)를 하나 눌러 하위 목록이 보이는 상태로 두고 [예]를 누르세요.\r\n(건너뛰려면 [아니오])" },
                new string[] { "3-manage-select", "'내 상품 관리' 화면(상품 목록 표)에서 상품 하나를 체크하고 [상품삭제] 버튼을 눌러 확인창이 뜬 상태로 두고 [예]를 누르세요.\r\n※ 확인창에서 절대 '삭제'를 누르지 마세요. 끝나면 '취소'를 누르세요.\r\n(건너뛰려면 [아니오])" }
            };
            foreach (var st in steps)
            {
                if (Ask == null || !Ask(st[1])) continue;
                Sleep(500);
                SavePage(dir, st[0], info);
            }
            File.WriteAllText(Path.Combine(dir, "info.txt"), info.ToString(), System.Text.Encoding.UTF8);

            string zip = dir + ".zip";
            try
            {
                if (File.Exists(zip)) File.Delete(zip);
                System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
            }
            catch (Exception ex) { Log.Write("zip 생성 실패: " + ex.Message); zip = dir; }
            return zip;
        }

        private void SavePage(string dir, string name, System.Text.StringBuilder info)
        {
            try
            {
                string url = SafeUrl();
                string title = ""; try { title = _driver.Title; } catch { }
                File.WriteAllText(Path.Combine(dir, name + ".html"), _driver.PageSource, System.Text.Encoding.UTF8);
                try { ((ITakesScreenshot)_driver).GetScreenshot().SaveAsFile(Path.Combine(dir, name + ".png")); } catch { }
                info.AppendLine(name + "  URL=" + url + "  TITLE=" + title);
                Log.Write("  저장: " + name + "  (" + url + ")");
            }
            catch (Exception ex) { Log.Write("  저장 실패(" + name + "): " + ex.Message); }
        }

        private bool IsLoggedIn()
        {
            try { return Find(_sel.LoggedInXPath, 1) != null; } catch { return false; }
        }

        private bool WaitLoggedIn(int seconds)
        {
            var end = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < end)
            {
                CheckStop();
                if (IsLoggedIn()) return true;
                Thread.Sleep(1000);
            }
            return false;
        }

        // =============== 상품 등록 ===============


        public bool Register(Product p)
        {
            Start();
            Log.Write("[등록] " + p.Title);
            try
            {
                if (!p.IsValid) throw new Exception("제목 또는 가격이 비어 있습니다.");
                if (Regex.Matches((p.Title ?? "") + (p.Description ?? ""), @"\?\?").Count >= 3)
                    throw new Exception("제목/설명에 '??' 가 여러 곳 있습니다. 상품 편집에서 제목과 설명을 확인해 주세요.");
                ShippingOptions.Validate(p);
                Go(_sel.NewProductUrl);
                Sleep(2000);
                // 사이트가 맨 위 검색창에 포커스를 줘서 검색 팝업이 열려 있을 수 있으니 포커스를 해제한다
                try { ((IJavaScriptExecutor)_driver).ExecuteScript("if(document.activeElement)document.activeElement.blur();"); } catch { }

                if (!WaitLoggedIn(5))
                    throw new RegistrationBlockedException("번개장터 로그인이 필요합니다. 크롬 창에서 로그인한 뒤 다시 시작해 주세요.");

                // 사진 (번개장터는 jpg/png 만 허용, 최대 12장)
                var files = (p.Images ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).Take(12).ToList();
                foreach (var f in files)
                {
                    if (!File.Exists(f)) throw new Exception("사진 파일이 없습니다: " + f);
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                        throw new Exception("jpg/png 사진만 올릴 수 있습니다: " + Path.GetFileName(f));
                }
                if (files.Count > 0)
                {
                    var input = Require(_sel.ImageInputXPath, "사진 입력칸");
                    input.SendKeys(string.Join("\n", files.ToArray()));
                    Sleep(2000 + 600 * files.Count);
                }

                SelectCategory(p.Category);
                var titleBox = FindTitleInput();
                SetText(titleBox, p.Title);
                ClearHeaderSearch();
                SelectCondition(p.Condition);
                SetText(Require(_sel.DescriptionXPath, "설명"), p.Description ?? "");

                // 태그: 띄어쓰기로 구분, 최대 5개, 태그당 9자
                var tagBox = Find(_sel.TagXPath, 2);
                if (tagBox != null && !string.IsNullOrWhiteSpace(p.Tags))
                {
                    int n = 0;
                    foreach (var t in p.Tags.Split(new char[] { ',', '#', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string tag = t.Trim();
                        if (tag.Length == 0) continue;
                        if (tag.Length > 9) tag = tag.Substring(0, 9);
                        tagBox.SendKeys(tag + " ");
                        Sleep(250);
                        if (++n >= 5) break;
                    }
                }

                SetText(Require(_sel.PriceXPath, "가격"), p.Price);

                ConfigureShipping(p);

                if (!string.IsNullOrWhiteSpace(p.Quantity) && p.Quantity.Trim() != "1")
                {
                    var q = Find(_sel.QuantityXPath, 2);
                    if (q != null) SetText(q, p.Quantity.Trim());
                }

                var submit = Require(_sel.SubmitXPath, "등록하기 버튼");
                CheckStop();
                try
                {
                    UsageQuota.RequireMode(_cfg, false);
                    // No shop lookup or account binding: EXP enforces the device total only.
                    if (!IsLoggedIn()) throw new InvalidOperationException("번개장터 로그인이 만료되었습니다. 다시 로그인해 주세요.");
                    UsageQuota.Consume();
                }
                catch (Exception blocked) { throw new RegistrationBlockedException(blocked.Message); }
                Click(submit);
                TryClick(_sel.DialogConfirmXPath, 2);   // 확인 팝업이 뜨면 누름

                // 등록 완료 = 주소가 상품 상세(/products/번호)로 바뀜
                string id = null;
                var end = DateTime.Now.AddSeconds(30);
                while (DateTime.Now < end)
                {
                    CheckStop();
                    string u = SafeUrl();
                    var m = Regex.Match(u ?? "", _sel.ProductIdRegex);
                    if (m.Success && !u.Contains("/new")) { id = m.Groups[1].Value; break; }
                    Thread.Sleep(500);
                }
                if (id == null)
                {
                    // 주소로 알 수 없으면 '내 상품 관리' 첫 줄에서 같은 제목을 찾는다
                    id = FindNewestIdByTitle(p.Title);
                }
                if (id == null)
                {
                    Shot("register-nourl");
                    throw new Exception("등록 완료를 확인하지 못했습니다. 화면에 오류 문구(필수 항목 등)가 없는지 확인해 주세요.");
                }

                p.ProductId = id;
                p.LastPosted = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                p.Status = "등록됨";
                Log.Write("  → 등록 완료 (상품번호 " + id + ")");
                return true;
            }
            catch (RegistrationBlockedException ex) { p.Status = "중단: " + ex.Message; throw; }
            catch (StopRequestedException) { throw; }
            catch (Exception ex)
            {
                p.Status = "실패: " + ex.Message;
                Log.Write("  → 실패: " + ex.Message);
                Shot("register-error");
                return false;
            }
        }


        // ---- 상품명 입력칸 찾기: 맨 위 검색창은 제외한다 ----
        private bool IsSearchInput(IWebElement e)
        {
            try
            {
                object r = ((IJavaScriptExecutor)_driver).ExecuteScript(
                    "var el=arguments[0];" +
                    "if(el.type==='search')return true;" +
                    "if((el.placeholder||'').indexOf('검색')>=0)return true;" +
                    "if(el.closest('[role=search]'))return true;" +
                    "return false;", e);
                return r is bool && (bool)r;
            }
            catch { return false; }
        }

        private void ConfigureShipping(Product p)
        {
            ShippingOptions.Validate(p);
            string expected=p.FreeShipping?"배송비 포함":"배송비 별도";
            var open=Require(_sel.ShippingOpenXPath,"배송비 포함 / 별도 선택");
            if(!open.Text.Replace(" ","").Contains(expected.Replace(" ",""))){
                Click(open);
                Click(Require("//*[@role='option'][translate(normalize-space(.),' ','')='"+expected.Replace(" ","")+"']",expected));
            }
            if(!Require(_sel.ShippingOpenXPath,"배송 방식").Text.Replace(" ","").Contains(expected.Replace(" ","")))
                throw new InvalidOperationException("배송 방식이 적용되지 않았습니다. 상품 등록을 중단했습니다.");
            const string section="//*[@id='scroll-trade-shippingSpecs']";
            if(!p.FreeShipping){
                SetShippingAmount(Require("("+section+"//input[@inputmode='numeric'])[1]","일반택배 배송비"),p.ShippingFee.Value,"일반택배");
                string radio="//input[@type='radio' and @name='isPossible' and @value='"+(p.HalfShipping?"1":"0")+"']";
                var choice=Require(radio,"반값택배 가능 여부");
                if(!choice.Selected)Click(choice.FindElement(By.XPath("ancestor::label[1]")));
                if(!Require(radio,"반값택배 가능 여부").Selected)
                    throw new InvalidOperationException("이 상품에 반값택배 설정을 적용할 수 없습니다. 번개장터의 상품 가격·배송 조건을 확인해 주세요.");
                if(p.HalfShipping){
                    string label=ShippingOptions.CarrierLabel(p.HalfShippingCarrier);
                    var carrier=Require(section+"//button[@aria-haspopup='listbox']","반값택배 편의점");
                    if(carrier.Text.Replace(" ","")!=label.Replace(" ","")){
                        Click(carrier);Click(Require("//*[@role='option'][translate(normalize-space(.),' ','')='"+label.Replace(" ","")+"']",label));
                    }
                    if(Require(section+"//button[@aria-haspopup='listbox']","반값택배 편의점").Text.Replace(" ","")!=label.Replace(" ",""))
                        throw new InvalidOperationException("반값택배 편의점 설정이 적용되지 않았습니다.");
                    SetShippingAmount(Require("("+section+"//input[@inputmode='numeric'])[2]","반값택배 요금"),p.HalfShippingFee.Value,"반값택배");
                }
            }
            var remember=Require("//input[@type='checkbox' and @id='default-shipping-fee'] | //label[contains(normalize-space(.),'다음 등록시에도 사용')]//input[@type='checkbox']","다음 등록시에도 사용");
            if(remember.Selected!=p.RememberShipping)Click(remember);
            if(remember.Selected!=p.RememberShipping)throw new InvalidOperationException("배송 기본값 저장 옵션을 적용하지 못했습니다.");
        }

        private void SetShippingAmount(IWebElement input,int amount,string caption)
        {
            var range=Regex.Match(input.GetAttribute("placeholder")??"",@"([\d,]+)\s*~\s*([\d,]+)");
            int min,max;
            if(range.Success&&int.TryParse(range.Groups[1].Value.Replace(",",""),out min)&&int.TryParse(range.Groups[2].Value.Replace(",",""),out max)&&(amount<min||amount>max))
                throw new InvalidOperationException(caption+" 요금은 현재 번개장터에서 "+min.ToString("N0")+"~"+max.ToString("N0")+"원으로 입력할 수 있습니다. 상품 편집에서 변경해 주세요.");
            SetText(input,amount.ToString());
            int actual;if(!int.TryParse((input.GetAttribute("value")??"").Replace(",",""),out actual)||actual!=amount)
                throw new InvalidOperationException(caption+" 배송비가 정확히 입력되지 않았습니다. 등록을 중단했습니다.");
        }

        private IWebElement FindTitleInput()
        {
            string[] xpaths = new string[]
            {
                _sel.TitleXPath,
                "//input[contains(@placeholder,'상품명') and not(contains(@placeholder,'검색'))]",
                "//*[@id='scroll-name' or @id='scroll-title']//input",
                "//main//input[(@type='text' or not(@type)) and not(contains(@placeholder,'검색'))]"
            };
            var end = DateTime.Now.AddSeconds(8);
            do
            {
                CheckStop();
                foreach (var xp in xpaths)
                {
                    if (string.IsNullOrEmpty(xp)) continue;
                    foreach (var e in SafeFind(xp))
                    {
                        try
                        {
                            if (!e.Displayed) continue;
                            if (IsSearchInput(e)) continue;
                            return e;
                        }
                        catch { }
                    }
                }
                Thread.Sleep(300);
            } while (DateTime.Now < end);

            // 못 찾으면 화면의 입력칸 목록을 로그에 남겨 원인 파악을 돕는다
            try
            {
                var names = new List<string>();
                foreach (var e in SafeFind("//input"))
                {
                    try { names.Add("[name=" + e.GetAttribute("name") + ", placeholder=" + e.GetAttribute("placeholder") + ", type=" + e.GetAttribute("type") + "]"); } catch { }
                }
                Log.Write("  화면의 입력칸: " + string.Join(" ", names.Take(20).ToArray()));
            }
            catch { }
            throw new Exception("상품명 입력칸을 화면에서 찾지 못했습니다. (맨 위 검색창은 제외하고 찾습니다)");
        }

        /// <summary>혹시 맨 위 검색창에 글자가 들어갔다면 비운다.</summary>
        private void ClearHeaderSearch()
        {
            try
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript(
                    "document.querySelectorAll('input').forEach(function(el){" +
                    "var isS=el.type==='search'||(el.placeholder||'').indexOf('검색')>=0||el.closest('[role=search]');" +
                    "if(isS&&el.value){var st=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value').set;" +
                    "st.call(el,'');el.dispatchEvent(new Event('input',{bubbles:true}));}});");
            }
            catch { }
        }


        private void ScrollToCategory()
        {
            try
            {
                var sec = SafeFind("//*[@id='scroll-categoryId']");
                if (sec.Count > 0)
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", sec[0]);
            }
            catch { }
        }

        /// <summary>카테고리 목록이 접혀 있으면 '변경/직접 선택' 같은 버튼을 눌러 다시 펼친다.</summary>
        private void ReopenCategory()
        {
            try
            {
                foreach (var e in SafeFind("//*[@id='scroll-categoryId']//button[contains(normalize-space(.),'변경') or contains(normalize-space(.),'직접') or contains(normalize-space(.),'다시') or contains(normalize-space(.),'선택')]"))
                {
                    try { if (e.Displayed) { Click(e); Sleep(600); return; } } catch { }
                }
            }
            catch { }
        }

        private void LogCategorySection()
        {
            try
            {
                var sec = SafeFind("//*[@id='scroll-categoryId']");
                if (sec.Count == 0) { Log.Write("  [카테고리 점검] id='scroll-categoryId' 영역이 화면에 없습니다."); return; }
                string t = "";
                try { t = sec[0].Text.Replace("\r", " ").Replace("\n", " / "); } catch { }
                if (t.Length > 200) t = t.Substring(0, 200);
                Log.Write("  [카테고리 점검] 영역 있음, 보이는 글자: " + t);
            }
            catch { }
        }


        private void SelectCategory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new Exception("카테고리가 비어 있습니다. (예: 디지털>휴대폰>애플)");
            if (!string.IsNullOrEmpty(_sel.CategoryOpenXPath))
            {
                var open = Find(_sel.CategoryOpenXPath, 3);
                if (open != null) { Click(open); Sleep(700); }
            }
            foreach (var part in path.Split('>'))
            {
                string name = part.Trim();
                if (name.Length == 0) continue;
                IWebElement item = null;
                var end = DateTime.Now.AddSeconds(20);
                int tries = 0;
                while (DateTime.Now < end && item == null)
                {
                    CheckStop();
                    item = FindCategoryItem(name);
                    if (item == null)
                    {
                        tries++;
                        ScrollToCategory();
                        if (tries % 4 == 0) ReopenCategory();
                        Thread.Sleep(500);
                    }
                }
                if (item == null)
                {
                    var seen = VisibleCategoryTexts();
                    Log.Write("  화면의 카테고리 후보: " + string.Join(" | ", seen.Take(40).ToArray()));
                    LogCategorySection();
                    throw new Exception("카테고리를 찾지 못했습니다: " + name);
                }
                Click(item);
                Sleep(800);
            }
        }

        private static string Norm(string s) { return Regex.Replace(s ?? "", @"\s+", ""); }

        /// <summary>공백/줄바꿈 차이를 무시하고, 정확히 같은 글자 → 포함 관계 순으로 가장 안쪽 요소를 고른다.</summary>
        private IWebElement FindCategoryItem(string name)
        {
            string target = Norm(name);
            IWebElement exact = null, partial = null;
            int exactLen = int.MaxValue, partLen = int.MaxValue;
            foreach (var e in SafeFind("//*[@id='scroll-categoryId']//*[self::li or self::button or self::a or self::label or self::span or self::div]"))
            {
                string t;
                try { if (!e.Displayed) continue; t = Norm(e.Text); } catch { continue; }
                if (t.Length == 0) continue;
                if (t == target) { if (t.Length <= exactLen) { exact = e; exactLen = t.Length; } }
                else if (t.Contains(target) || target.Contains(t))
                {
                    if (t.Length < partLen) { partial = e; partLen = t.Length; }
                }
            }
            return exact ?? partial;
        }

        private List<string> VisibleCategoryTexts()
        {
            var list = new List<string>();
            foreach (var e in SafeFind("//*[@id='scroll-categoryId']//li"))
            {
                try { if (e.Displayed) { string t = e.Text.Trim().Replace("\n", " "); if (t.Length > 0 && !list.Contains(t)) list.Add(t); } } catch { }
            }
            return list;
        }

        private void SelectCondition(string cond)
        {
            if (string.IsNullOrWhiteSpace(cond)) throw new Exception("상품 상태가 비어 있습니다. (예: 새 상품(미사용), 사용감 없음, 사용감 적음, 사용감 많음)");
            var open = Require(_sel.ConditionOpenXPath, "상품상태 선택칸");
            Click(open);
            Sleep(600);
            if (!TryClick(SelectorSet.Fill(_sel.ConditionOptionXPath, cond.Replace(" ", "")), 4))
                throw new Exception("상품 상태 항목을 찾지 못했습니다: " + cond + "  (화면의 목록에 있는 글자 일부를 그대로 적어 주세요)");
            Sleep(400);
        }

        /// <summary>내 상품 관리 첫 페이지에서 제목이 같은 가장 위 상품의 번호를 돌려준다.</summary>
        private string FindNewestIdByTitle(string title)
        {
            try
            {
                Go(string.Format(_sel.ManageUrl, 0));
                Sleep(2500);
                foreach (var row in SafeFind(_sel.RowXPath))
                {
                    string t = ""; string href = "";
                    try { t = row.FindElement(By.XPath(_sel.RowTitleXPath)).Text.Trim(); } catch { }
                    try { href = row.FindElement(By.XPath(_sel.RowLinkXPath)).GetAttribute("href"); } catch { }
                    if (t == title.Trim() && href != null)
                    {
                        var m = Regex.Match(href, _sel.ProductIdRegex);
                        if (m.Success) return m.Groups[1].Value;
                    }
                }
            }
            catch (StopRequestedException) { throw; }
            catch { }
            return null;
        }

        // =============== 점검 도구 ===============
        /// <summary>등록 화면에서 각 XPath 가 실제로 찾아지는지 확인 (화면이 바뀌었는지 점검)</summary>
        public List<string> CheckSelectors()
        {
            Start();
            var report = new List<string>();
            Go(_sel.NewProductUrl);
            Sleep(2500);
            foreach (var name in SelectorSet.CheckOnNewProduct)
            {
                string xp = SelectorSet.GetField(_sel, name);
                int n = 0;
                try { n = _driver.FindElements(By.XPath(xp)).Count; } catch { }
                report.Add((n > 0 ? "OK   " : "없음 ") + name + "  (" + n + "개)");
            }
            Shot("selector-check");
            return report;
        }

        // =============== 도우미 ===============
        private void Go(string url)
        {
            CheckStop();
            try { _driver.Navigate().GoToUrl(url); }
            catch (WebDriverTimeoutException) { Log.Write("페이지 로딩이 오래 걸립니다. 계속 진행합니다."); }
        }

        private string SafeUrl() { try { return _driver.Url; } catch { return ""; } }

        private IWebElement Require(string xpath, string what)
        {
            var e = Find(xpath, 8);
            if (e == null) throw new Exception(what + " 을(를) 화면에서 찾지 못했습니다. (selectors.json 을 확인하세요)");
            return e;
        }

        private IWebElement Find(string xpath, int timeoutSec)
        {
            var end = DateTime.Now.AddSeconds(Math.Max(timeoutSec, 0));
            do
            {
                CheckStop();
                try
                {
                    var list = _driver.FindElements(By.XPath(xpath));
                    foreach (var e in list)
                    {
                        try { if (e.Displayed || e.TagName == "input") return e; } catch { }
                    }
                    if (list.Count > 0 && timeoutSec <= 1) return list[0];
                }
                catch (StopRequestedException) { throw; }
                catch (WebDriverException) { }
                if (timeoutSec > 0) Thread.Sleep(300);
            } while (DateTime.Now < end);
            return null;
        }

        private List<IWebElement> SafeFind(string xpath)
        {
            try { return _driver.FindElements(By.XPath(xpath)).ToList(); } catch { return new List<IWebElement>(); }
        }

        private bool TryClick(string xpath, int timeoutSec)
        {
            var e = Find(xpath, timeoutSec);
            if (e == null) return false;
            try { Click(e); return true; } catch { return false; }
        }

        private void Click(IWebElement e)
        {
            CheckStop();
            try { ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", e); } catch { }
            try { e.Click(); }
            catch (ElementClickInterceptedException) { ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", e); }
            catch (ElementNotInteractableException) { ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", e); }
        }


        private bool IsActive(IWebElement e)
        {
            try
            {
                object r = ((IJavaScriptExecutor)_driver).ExecuteScript("return document.activeElement===arguments[0];", e);
                return r is bool && (bool)r;
            }
            catch { return false; }
        }

        private static bool HasNonBmp(string t)
        {
            foreach (char c in t) if (char.IsSurrogate(c)) return true;
            return false;
        }

        private static string NormNl(string t) { return (t ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim(); }

        private string ValueOf(IWebElement e)
        {
            try { return e.GetAttribute("value") ?? ""; } catch { return ""; }
        }

        /// <summary>
        /// 입력칸에 글자를 넣고, 실제로 들어갔는지 확인한다.
        /// 1) 이모지가 없으면 실제 키 입력 → 2) 안 들어가면 JS 로 값 설정 + 입력 이벤트 → 3) 그래도 다르면 경고
        /// </summary>
        private void SetText(IWebElement e, string text)
        {
            CheckStop();
            text = text ?? "";
            bool multiline = false;
            try { multiline = e.TagName == "textarea"; } catch { }
            string want = NormNl(text);

            if (!HasNonBmp(text) && !multiline)
            {
                try
                {
                    e.Click();
                    if (!IsActive(e)) throw new Exception("입력칸에 포커스가 없음");
                    e.SendKeys(Keys.Control + "a");
                    e.SendKeys(Keys.Delete);
                    e.SendKeys(text);
                    Sleep(150);
                    if (NormNl(ValueOf(e)) == want) return;
                }
                catch { }
            }

            // JS 로 값 설정 (React 가 변경을 알도록 네이티브 setter + 이벤트)
            string js =
                "var el=arguments[0],v=arguments[1];" +
                "el.focus();" +
                "var proto=el.tagName==='TEXTAREA'?window.HTMLTextAreaElement.prototype:window.HTMLInputElement.prototype;" +
                "var setter=Object.getOwnPropertyDescriptor(proto,'value').set;" +
                "setter.call(el,v);" +
                "el.dispatchEvent(new Event('input',{bubbles:true}));" +
                "el.dispatchEvent(new Event('change',{bubbles:true}));";
            try { ((IJavaScriptExecutor)_driver).ExecuteScript(js, e, text); } catch { }
            Sleep(150);
            string got = NormNl(ValueOf(e));
            if (got == want)
            {
                // 일부 입력칸은 실제 키 입력이 있어야 상태가 갱신되므로 공백 입력/삭제로 한 번 더 알려준다
                try { if (IsActive(e)) { e.SendKeys(" "); e.SendKeys(Keys.Backspace); } } catch { }
                return;
            }
            // 가격 등 숫자 입력칸은 화면에서 천 단위 쉼표를 자동으로 붙여 글자 수가 달라 보일 수 있다. 숫자만 비교해서 같으면 정상으로 본다.
            string wantDigits = new string(want.Where(char.IsDigit).ToArray());
            string gotDigits = new string(got.Where(char.IsDigit).ToArray());
            if (wantDigits.Length > 0 && wantDigits == gotDigits) return;
            Log.Write("  [경고] 입력칸에 값이 정확히 들어가지 않았습니다. (넣으려던 글자 수 " + want.Length + ", 실제 " + got.Length + ")");
        }

        // 작업과 작업 사이에 쉬는 시간(초). 이 범위 안에서 매번 무작위로 정한다.
        private const int PauseMinSec = 3;
        private const int PauseMaxSec = 8;

        public void HumanPause()
        {
            int ms = _rnd.Next(PauseMinSec * 1000, PauseMaxSec * 1000 + 1);
            Sleep(ms);
        }

        private void Sleep(int ms)
        {
            int waited = 0;
            while (waited < ms)
            {
                CheckStop();
                int step = Math.Min(200, ms - waited);
                Thread.Sleep(step);
                waited += step;
            }
        }

        private void CheckStop()
        {
            if (StopRequested) throw new StopRequestedException();
            while (PauseRequested)
            {
                if (StopRequested) throw new StopRequestedException();
                Thread.Sleep(200);
            }
        }

        private void Shot(string name)
        {
            try
            {
                Directory.CreateDirectory(Paths.Shots);
                string f = Path.Combine(Paths.Shots, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + name + ".png");
                ((ITakesScreenshot)_driver).GetScreenshot().SaveAsFile(f);
                Log.Write("  (화면 캡처 저장: " + f + ")");
            }
            catch { }
        }
    }
}
