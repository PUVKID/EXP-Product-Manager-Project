using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    public static class Storage
    {
        // ---------- 설정 ----------
        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(Paths.Settings))
                {
                    var s = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(Paths.Settings, Encoding.UTF8));
                    if (s != null) return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings s)
        {
            Directory.CreateDirectory(Paths.Root);
            File.WriteAllText(Paths.Settings, JsonConvert.SerializeObject(s, Formatting.Indented), Encoding.UTF8);
        }

        // ---------- 비밀번호 보호 (Windows DPAPI: 이 PC, 이 사용자에서만 복호화 가능) ----------
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch { return ""; }
        }

        public static string Unprotect(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return "";
            try
            {
                byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }

        // ---------- 상품 목록 자동 저장/복원 ----------
        public static BindingList<Product> LoadProducts()
        {
            try
            {
                if (File.Exists(Paths.Products))
                {
                    var list = JsonConvert.DeserializeObject<List<Product>>(File.ReadAllText(Paths.Products, Encoding.UTF8));
                    if (list != null) return new BindingList<Product>(list);
                }
            }
            catch { }
            return new BindingList<Product>();
        }

        public static void SaveProducts(IList<Product> items)
        {
            try
            {
                Directory.CreateDirectory(Paths.Root);
                var tmp = Paths.Products + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(new List<Product>(items), Formatting.Indented), Encoding.UTF8);
                if (File.Exists(Paths.Products)) File.Delete(Paths.Products);
                File.Move(tmp, Paths.Products);
            }
            catch { }
        }
    }

    /// <summary>엑셀에서 열 수 있는 CSV(UTF-8 BOM) 읽기/쓰기. 따옴표, 줄바꿈 포함 필드를 지원한다.</summary>
    public static class CsvIO
    {
        public static readonly string[] Headers = new string[]
        {
            "제목", "가격", "카테고리", "상태", "수량", "태그", "이미지", "설명", "배송비포함"
        };

        public static List<Product> Read(string path)
        {
            string text = ReadAllTextAuto(path);
            var rows = Parse(text);
            var result = new List<Product>();
            if (rows.Count == 0) return result;

            // 헤더 이름으로 열 위치를 찾는다(순서가 달라도 OK)
            var head = rows[0];
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < head.Count; i++) idx[head[i].Trim()] = i;
            Func<List<string>, string, string> get = (r, name) =>
            {
                int k;
                if (!idx.TryGetValue(name, out k) || k >= r.Count) return "";
                return r[k];
            };

            string baseDir = Path.GetDirectoryName(Path.GetFullPath(path));
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count == 0 || (r.Count == 1 && string.IsNullOrWhiteSpace(r[0]))) continue;
                var p = new Product();
                p.Title = get(r, "제목").Trim();
                p.Price = get(r, "가격").Replace(",", "").Replace("원", "").Trim();
                p.Category = get(r, "카테고리").Trim();
                string cond = get(r, "상태").Trim();
                p.Condition = cond;
                string qty = get(r, "수량").Trim();
                p.Quantity = qty.Length > 0 ? qty : "1";
                p.Tags = get(r, "태그").Trim();
                p.Description = get(r, "설명");
                string ship = get(r, "배송비포함").Trim().ToUpperInvariant();
                p.FreeShipping = ship == "Y" || ship == "O" || ship == "TRUE" || ship == "1" || ship == "예";

                // 이미지 경로는 CSV 파일 위치 기준 상대경로도 허용
                var imgs = new List<string>();
                foreach (var part in get(r, "이미지").Split(new char[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = part.Trim().Trim('"');
                    if (t.Length == 0) continue;
                    imgs.Add(Path.IsPathRooted(t) ? t : Path.GetFullPath(Path.Combine(baseDir, t)));
                }
                p.Images = string.Join(";", imgs.ToArray());
                result.Add(p);
            }
            return result;
        }

        /// <summary>
        /// 엑셀 'CSV(쉼표로 분리)'는 CP949, 'CSV UTF-8'은 UTF-8 이라 둘 다 읽을 수 있게 인코딩을 자동 판별한다.
        /// </summary>
        public static string ReadAllTextAuto(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(b); }   // 올바른 UTF-8 이면 그대로
            catch (DecoderFallbackException) { }
            try { return Encoding.GetEncoding(949).GetString(b); }        // 아니면 한국어 Windows(CP949)
            catch { return Encoding.Default.GetString(b); }
        }

        public static void Write(string path, IEnumerable<Product> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Headers));
            foreach (var p in items)
            {
                sb.AppendLine(string.Join(",", new string[]
                {
                    Q(p.Title), Q(p.Price), Q(p.Category), Q(p.Condition), Q(p.Quantity),
                    Q(p.Tags), Q(p.Images), Q(p.Description), p.FreeShipping ? "Y" : "N"
                }));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static void WriteTemplate(string path)
        {
            var sample = new Product
            {
                Title = "아이폰 15 프로 256GB 블랙 티타늄",
                Price = "1050000",
                Category = "디지털>휴대폰>애플",
                Condition = "사용감 적음",
                Quantity = "1",
                Tags = "아이폰,아이폰15프로,애플",
                Images = "photos\\iphone1.jpg;photos\\iphone2.png",
                Description = "구매한 지 6개월, 생활기스 거의 없습니다.\n풀박스 구성입니다.",
                FreeShipping = true
            };
            Write(path, new Product[] { sample });
        }

        private static string Q(string s)
        {
            if (s == null) s = "";
            if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cur = new StringBuilder();
            bool inQ = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQ = false;
                    }
                    else cur.Append(c);
                }
                else
                {
                    if (c == '"') inQ = true;
                    else if (c == ',') { row.Add(cur.ToString()); cur.Length = 0; }
                    else if (c == '\r') { }
                    else if (c == '\n') { row.Add(cur.ToString()); cur.Length = 0; rows.Add(row); row = new List<string>(); }
                    else cur.Append(c);
                }
            }
            if (cur.Length > 0 || row.Count > 0) { row.Add(cur.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
