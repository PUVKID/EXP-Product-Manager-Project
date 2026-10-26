using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    /// <summary>등록할 상품 한 건. DataGridView에 바인딩된다.</summary>
    public class Product : INotifyPropertyChanged
    {
        private string _title = "";
        private string _price = "";
        private string _category = "";
        private string _condition = "";
        private string _quantity = "1";
        private string _tags = "";
        private string _images = "";
        private string _description = "";
        private bool _freeShipping = false;
        private string _status = "";
        private string _productId = "";
        private string _lastPosted = "";

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) { var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(n)); }

        public string Title { get { return _title; } set { _title = value; Raise("Title"); } }
        public string Price { get { return _price; } set { _price = value; Raise("Price"); } }
        /// <summary>"대분류>중분류>소분류" 형태</summary>
        public string Category { get { return _category; } set { _category = value; Raise("Category"); } }
        /// <summary>새 상품 / 중고 등 화면에 표시되는 글자 그대로</summary>
        public string Condition { get { return _condition; } set { _condition = value; Raise("Condition"); } }
        public string Quantity { get { return _quantity; } set { _quantity = value; Raise("Quantity"); } }
        /// <summary>쉼표로 구분</summary>
        public string Tags { get { return _tags; } set { _tags = value; Raise("Tags"); } }
        /// <summary>사진 경로, ; 로 구분 (최대 12장)</summary>
        public string Images { get { return _images; } set { _images = value; Raise("Images"); } }
        public string Description { get { return _description; } set { _description = value; Raise("Description"); } }
        public bool FreeShipping { get { return _freeShipping; } set { _freeShipping = value; Raise("FreeShipping"); } }
        public int? ShippingFee { get; set; }
        public bool HalfShipping { get; set; }
        public string HalfShippingCarrier { get; set; }
        public int? HalfShippingFee { get; set; }
        public bool RememberShipping { get; set; }

        // ----- 실행 결과(프로그램이 채움) -----
        public string Status { get { return _status; } set { _status = value; Raise("Status"); } }
        public string ProductId { get { return _productId; } set { _productId = value; Raise("ProductId"); } }
        public string LastPosted { get { return _lastPosted; } set { _lastPosted = value; Raise("LastPosted"); } }

        [JsonIgnore]
        public bool IsValid
        {
            get { return !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Price); }
        }
    }

    public class AppSettings
    {
        public bool DoRegister = true;
        /// <summary>true 이면 목록을 다 올린 뒤 처음 상품부터 다시 올린다(총 RepeatCount 회).</summary>
        public bool RepeatAll = false;
        /// <summary>목록 전체를 올리는 총 횟수(처음 1회 포함).</summary>
        public int RepeatCount = 2;
        /// <summary>true 이면 유효한 전체 목록을 즉시 올린 뒤 중지할 때까지 정해진 시각에 반복한다.</summary>
        public bool BatchRepeatEnabled = false;
        /// <summary>true 이면 매시 지정분, false 이면 시작 시각 기준 분 간격으로 전체 목록을 반복한다.</summary>
        public bool BatchRepeatClockAligned = false;
        public int BatchRepeatMinutes = 60;
        public int BatchRepeatMinuteOfHour = 0;
        /// <summary>true 이면 등록을 1개씩, OneByOneMinutes 분 간격(또는 시간대별 간격)으로 진행하고 모두 끝나면 자동 종료한다.</summary>
        public bool OneByOne = true;
        public int OneByOneMinutes = 20;
        /// <summary>true 이면 OneByOneMinutes 대신 TimeRules(시간대별 간격)대로 등록한다.</summary>
        public bool UseTimeRules = false;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<TimeRule> TimeRules = new List<TimeRule>();
        /// <summary>true 이면 이미 등록된 상품도 (중복되더라도) 목록 순서대로 다시 등록한다.</summary>
        public bool AllowDuplicate = false;
        public string LastCsv = "";
        public string ChromePath = "";
        /// <summary>사용자가 한 번 "로그인 완료"를 확인했으면 true. 이후에는 로그인된 것으로 보고 진행한다.</summary>
        public bool AssumeLoggedIn = false;
        /// <summary>화면 테마: Light / Dark / System (v2). 예전 settings.json 에는 없으므로 기본값 System.</summary>
        public string ThemeMode = "System";
    }

    public static class Paths
    {
#if TESTBUILD
        public static readonly string Root = Path.Combine(Path.GetTempPath(), "Lumivolt-tests-" + System.Diagnostics.Process.GetCurrentProcess().Id);
#else
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BunjangAuto");
#endif
        public static string Settings { get { return Path.Combine(Root, "settings.json"); } }
        public static string Products { get { return Path.Combine(Root, "products.json"); } }
        public static string Selectors { get { return Path.Combine(Root, "selectors.json"); } }
        public static string Profile { get { return Path.Combine(AsciiBase, "chrome-profile"); } }

        /// <summary>
        /// 크롬/chromedriver 가 쓰는 폴더는 영문 경로여야 안전하다.
        /// (윈도우 사용자 이름이 한글이면 %AppData% 경로 때문에 크롬이 시작 직후 종료되는 경우가 있음)
        /// </summary>
        public static string AsciiBase
        {
            get
            {
                if (IsAscii(Root)) return Root;
                string pd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BunjangAuto");
                if (IsAscii(pd)) return pd;
                return @"C:\BunjangAuto";
            }
        }

        public static bool IsAscii(string s)
        {
            foreach (char c in s) if (c > 127) return false;
            return true;
        }
        public static string Drivers { get { return Path.Combine(AsciiBase, "drivers"); } }
        public static string Logs { get { return Path.Combine(Root, "logs"); } }
        public static string Shots { get { return Path.Combine(Root, "screenshots"); } }

        public static void EnsureAll()
        {
            Directory.CreateDirectory(Root);
            try { Directory.CreateDirectory(Drivers); } catch { }
            Directory.CreateDirectory(Logs);
            Directory.CreateDirectory(Shots);
        }
    }
}
