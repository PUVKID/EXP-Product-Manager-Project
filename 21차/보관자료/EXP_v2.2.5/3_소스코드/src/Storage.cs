using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BunjangAuto
{
    public static class Storage
    {
        // ---------- 설정 (이 PC에서만 열리도록 암호화해 저장) ----------
        public static AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(Paths.Settings)) return new AppSettings();
                string raw = File.ReadAllText(Paths.Settings, Encoding.UTF8);
                string json;
                bool legacy = !Vault.IsProtected(raw);
                try { json = legacy ? raw : Vault.Unprotect(raw, "settings"); }
                catch (CryptographicException)
                {
                    Log.Write("[주의] 설정 파일을 열 수 없어(다른 PC에서 옮겨왔거나 손상됨) 기본값으로 시작합니다.");
                    return new AppSettings();
                }
                var s = JsonConvert.DeserializeObject<AppSettings>(json);
                if (s == null) return new AppSettings();
                if (legacy) { try { SaveSettings(s); } catch { } }   // 예전 평문 설정을 암호화 형식으로 올려 쓴다
                return s;
            }
            catch { return new AppSettings(); }
        }

        public static void SaveSettings(AppSettings s)
        {
            Directory.CreateDirectory(Paths.Root);
            string json = JsonConvert.SerializeObject(s, Formatting.Indented);
            File.WriteAllText(Paths.Settings, Vault.Protect(json, "settings"), Encoding.UTF8);
        }

        // ---------- 상품 목록 자동 저장/복원 (이 PC에서만 열리도록 암호화해 저장) ----------
        public static BindingList<Product> LoadProducts()
        {
            try
            {
                if (!File.Exists(Paths.Products)) return new BindingList<Product>();
                string raw = File.ReadAllText(Paths.Products, Encoding.UTF8);
                string json;
                bool legacy = !Vault.IsProtected(raw);
                try { json = legacy ? raw : Vault.Unprotect(raw, "products"); }
                catch (CryptographicException)
                {
                    Log.Write("[주의] 상품 목록 파일을 열 수 없습니다(다른 PC에서 옮겨왔거나 손상됨). " +
                              "관리자에게 데이터 복구를 문의해 주세요.");
                    return new BindingList<Product>();
                }
                var list = JsonConvert.DeserializeObject<List<Product>>(json);
                if (list == null) return new BindingList<Product>();
                var bl = new BindingList<Product>(list);
                if (legacy) { try { SaveProducts(bl); } catch { } }   // 예전 평문 목록을 암호화 형식으로 올려 쓴다
                return bl;
            }
            catch { return new BindingList<Product>(); }
        }

        public static void SaveProducts(IList<Product> items)
        {
            try
            {
                Directory.CreateDirectory(Paths.Root);
                var tmp = Paths.Products + ".tmp";
                string json = JsonConvert.SerializeObject(new List<Product>(items), Formatting.Indented);
                File.WriteAllText(tmp, Vault.Protect(json, "products"), Encoding.UTF8);
                if (File.Exists(Paths.Products)) File.Delete(Paths.Products);
                File.Move(tmp, Paths.Products);
            }
            catch { }
        }
    }

    /// <summary>
    /// 엑셀·구글시트·메모장 등 어디서 저장한 CSV 든 글자가 깨지지 않게 읽고,
    /// 엑셀에서 바로 열리는 CSV(UTF-8 BOM)로 저장한다. 따옴표, 줄바꿈 포함 필드를 지원한다.
    /// </summary>
    public static class CsvIO
    {
        public static readonly string[] Headers = new string[]
        {
            "제목", "가격", "카테고리", "상태", "수량", "태그", "이미지", "설명", "배송비포함"
        };

        // 머리글 이름이 조금 달라도(상품명/판매가/영문 등) 같은 열로 인식한다.
        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            { "제목",      new string[] { "제목", "상품명", "상품제목", "이름", "title", "name", "productname" } },
            { "가격",      new string[] { "가격", "판매가", "판매가격", "금액", "price" } },
            { "카테고리",  new string[] { "카테고리", "카테고리명", "분류", "category" } },
            { "상태",      new string[] { "상태", "상품상태", "condition" } },
            { "수량",      new string[] { "수량", "재고", "재고수량", "quantity", "qty" } },
            { "태그",      new string[] { "태그", "키워드", "해시태그", "tags", "tag" } },
            { "이미지",    new string[] { "이미지", "이미지경로", "사진", "사진경로", "images", "image", "photo", "photos" } },
            { "설명",      new string[] { "설명", "상품설명", "내용", "본문", "description", "desc" } },
            { "배송비포함", new string[] { "배송비포함", "배송비", "택배비포함", "무료배송", "shipping", "freeshipping" } }
        };

        public static List<Product> Read(string path)
        {
            string text = ReadAllTextAuto(path);
            var rows = Parse(text, DetectDelimiter(text));
            var result = new List<Product>();
            if (rows.Count == 0) return result;

            // 머리글 이름으로 열 위치를 찾는다(순서가 달라도 OK)
            var head = rows[0];
            var idx = new Dictionary<string, int>();
            foreach (var kv in Aliases)
            {
                for (int i = 0; i < head.Count; i++)
                {
                    string h = NormHeader(head[i]);
                    if (Array.IndexOf(kv.Value, h) >= 0) { idx[kv.Key] = i; break; }
                }
            }
            if (!idx.ContainsKey("제목"))
                throw new InvalidDataException("CSV 첫 줄(머리글)에서 '제목' 열을 찾지 못했습니다.\r\n" +
                    "양식(CSV 양식 만들기)과 같은 머리글인지 확인해 주세요.\r\n" +
                    "글자가 깨져 보인다면 엑셀에서 'CSV UTF-8(쉼표로 분리)'로 다시 저장해 주세요.");
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
                if (r.Count == 0 || r.All(x => string.IsNullOrWhiteSpace(x))) continue;
                var p = new Product();
                p.Title = Clean(get(r, "제목")).Trim();
                p.Price = CleanNumber(get(r, "가격"));
                p.Category = Clean(get(r, "카테고리")).Trim();
                p.Condition = Clean(get(r, "상태")).Trim();
                string qty = CleanNumber(get(r, "수량"));
                p.Quantity = qty.Length > 0 ? qty : "1";
                p.Tags = Clean(get(r, "태그")).Trim();
                p.Description = Clean(get(r, "설명")).Replace("\r\n", "\n").Replace('\r', '\n');
                string ship = Clean(get(r, "배송비포함")).Trim().ToUpperInvariant();
                p.FreeShipping = ship == "Y" || ship == "O" || ship == "TRUE" || ship == "1" || ship == "예" || ship == "포함";

                // 이미지 경로는 CSV 파일 위치 기준 상대경로도 허용
                var imgs = new List<string>();
                foreach (var part in Clean(get(r, "이미지")).Split(new char[] { ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string t = part.Trim().Trim('"');
                    if (t.Length == 0) continue;
                    try { imgs.Add(Path.IsPathRooted(t) ? t : Path.GetFullPath(Path.Combine(baseDir, t))); }
                    catch { imgs.Add(t); }
                }
                p.Images = string.Join(";", imgs.ToArray());
                result.Add(p);
            }
            return result;
        }

        /// <summary>눈에 안 보이는 글자(BOM, 폭 없는 공백, 줄바꿈 아닌 NUL 등)를 없앤다.</summary>
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\uFEFF", "").Replace("\u200B", "").Replace("\0", "");
        }

        private static string NormHeader(string h)
        {
            h = Clean(h).Trim().Trim('"');
            h = System.Text.RegularExpressions.Regex.Replace(h, @"[\(（\[].*?[\)）\]]", "");   // "카테고리 (대>중>소)" → "카테고리"
            h = System.Text.RegularExpressions.Regex.Replace(h, @"\s+", "");
            return h.ToLowerInvariant();
        }

        /// <summary>"₩1,050,000원", "1.050.000", "1050000.0" 처럼 적혀 있어도 숫자만 남긴다.</summary>
        private static string CleanNumber(string s)
        {
            s = Clean(s).Trim();
            if (s.Length == 0) return "";
            string t = s.Replace(",", "").Replace("원", "").Replace("₩", "").Replace("￦", "").Replace("\\", "").Replace(" ", "").Replace("\u00A0", "");
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d{1,3}(\.\d{3})+$")) t = t.Replace(".", "");   // 1.050.000
            var m = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+)(\.0+)?$");                                   // 1050000.0
            return m.Success ? m.Groups[1].Value : s;
        }

        /// <summary>
        /// 파일을 읽어 인코딩을 자동 판별한다.
        /// UTF-8(BOM 있음/없음), UTF-16·UTF-32(엑셀 '유니코드 텍스트'), CP949/EUC-KR(엑셀 기본 'CSV') 모두 지원.
        /// 엑셀에서 열어 둔 파일도 읽을 수 있게 공유 읽기로 연다.
        /// </summary>
        public static string ReadAllTextAuto(string path)
        {
            byte[] b;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                b = ms.ToArray();
            }
            return DecodeAuto(b);
        }

        public static string DecodeAuto(byte[] b)
        {
            if (b == null || b.Length == 0) return "";
            string text = DecodeCore(b);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        private static string DecodeCore(byte[] b)
        {
            // 1) BOM 이 있으면 그대로 따른다
            if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0 && b[3] == 0) return new UTF32Encoding(false, false).GetString(b, 4, b.Length - 4);
            if (b.Length >= 4 && b[0] == 0 && b[1] == 0 && b[2] == 0xFE && b[3] == 0xFF) return new UTF32Encoding(true, false).GetString(b, 4, b.Length - 4);
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);

            // 2) BOM 없는 UTF-16: 쉼표·줄바꿈 같은 영문 글자 옆에 0x00 이 규칙적으로 나온다
            int n = Math.Min(b.Length, 4000), zeroEven = 0, zeroOdd = 0;
            for (int i = 0; i < n; i++) if (b[i] == 0) { if (i % 2 == 0) zeroEven++; else zeroOdd++; }
            if (zeroEven + zeroOdd > n / 10)
                return (zeroOdd >= zeroEven ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(b);

            // 3) 올바른 UTF-8 이면 UTF-8
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { }

            // 4) 아니면 한국어 Windows(CP949 / EUC-KR). 일부 글자가 안 읽히는 파일이면 깨진 글자가 더 적은 쪽을 고른다
            string kr = null;
            try { kr = Encoding.GetEncoding(949).GetString(b); } catch { }
            string u8 = Encoding.UTF8.GetString(b);
            if (kr == null) return u8;
            return CountBad(kr) <= CountBad(u8) ? kr : u8;
        }

        private static int CountBad(string s)
        {
            int c = 0;
            foreach (char ch in s) if (ch == '\uFFFD') c++;
            return c;
        }

        /// <summary>첫 줄에서 쉼표 / 탭 / 세미콜론 중 가장 많이 쓰인 구분 글자를 고른다.</summary>
        private static char DetectDelimiter(string text)
        {
            int comma = 0, tab = 0, semi = 0;
            bool inQ = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') inQ = !inQ;
                else if (!inQ)
                {
                    if (c == '\n' || c == '\r') break;
                    if (c == ',') comma++;
                    else if (c == '\t') tab++;
                    else if (c == ';') semi++;
                }
            }
            if (tab > comma && tab >= semi) return '\t';
            if (semi > comma && semi > tab) return ';';
            return ',';
        }

        public static void Write(string path, IEnumerable<Product> items)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Headers)).Append("\r\n");
            foreach (var p in items)
            {
                sb.Append(string.Join(",", new string[]
                {
                    Q(p.Title), Q(p.Price), Q(p.Category), Q(p.Condition), Q(p.Quantity),
                    Q(p.Tags), Q(p.Images), Q(p.Description), p.FreeShipping ? "Y" : "N"
                })).Append("\r\n");
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));   // 엑셀이 한글을 바로 인식하는 UTF-8 BOM
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

        private static List<List<string>> Parse(string text, char delim)
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
                    else if (c == delim) { row.Add(cur.ToString()); cur.Length = 0; }
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
