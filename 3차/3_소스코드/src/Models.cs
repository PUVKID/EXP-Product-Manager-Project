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

    public enum JobKind { Register, Bump, Repost }

    public class AppSettings
    {
        public string Id = "";
        /// <summary>DPAPI(현재 Windows 사용자)로 암호화한 비밀번호(Base64)</summary>
        public string PwProtected = "";
        public bool SaveAccount = true;
        public bool Headless = false;
        public int DelayMinSec = 3;
        public int DelayMaxSec = 7;
        public int MaxPerRun = 30;
        public bool DoRegister = true;
        public bool DoBump = false;
        public bool DoRepost = false;
        public bool Repeat = false;
        public int IntervalMinutes = 60;
        /// <summary>true 이면 신규 등록을 1개씩, OneByOneMinutes 분 간격으로 진행하고 모두 끝나면 자동 종료한다.</summary>
        public bool OneByOne = true;
        public int OneByOneMinutes = 20;
        /// <summary>true 이면 이미 등록된 상품도 (중복되더라도) 목록 순서대로 다시 등록한다.</summary>
        public bool AllowDuplicate = false;
        public string LastCsv = "";
        public string ChromePath = "";
        /// <summary>사용자가 한 번 "로그인 완료"를 확인했으면 true. 이후에는 로그인된 것으로 보고 진행한다.</summary>
        public bool AssumeLoggedIn = false;
    }

    public static class Paths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BunjangAuto");
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
