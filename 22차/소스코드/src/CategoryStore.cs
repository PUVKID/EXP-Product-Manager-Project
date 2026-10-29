using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BunjangAuto
{
    public class CatNode
    {
        public string Id = "";
        public string Title = "";
        public List<CatNode> Children = new List<CatNode>();
    }

    /// <summary>번개장터 공개 카테고리 목록을 내려받아 캐시(categories.json)해 두고 대>중>소 선택에 쓴다.</summary>
    public static class CategoryStore
    {
        private static List<CatNode> _roots;
        public static string LastError = "";
        private static string CachePath { get { return Path.Combine(Paths.Root, "categories.json"); } }

        public static List<CatNode> Roots(bool refresh)
        {
            if (_roots != null && !refresh) return _roots;
            string json = null;
            if (!refresh && File.Exists(CachePath))
                try { json = File.ReadAllText(CachePath, Encoding.UTF8); } catch { }
            if (json == null)
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls;
                    using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                        json = wc.DownloadString("https://api.bunjang.co.kr/api/1/categories/list.json");
                    }
                    Directory.CreateDirectory(Paths.Root);
                    File.WriteAllText(CachePath, json, Encoding.UTF8);
                    LastError = "";
                }
                catch (Exception ex) { LastError = ex.Message; }
            }
            var list = new List<CatNode>();
            if (json != null)
                try
                {
                    JToken t = JToken.Parse(json);
                    JArray arr = t as JArray;
                    if (arr == null && t is JObject) arr = FindChildren((JObject)t);
                    if (arr != null) foreach (var o in arr.OfType<JObject>()) list.Add(Parse(o));
                }
                catch (Exception ex) { LastError = ex.Message; }
            _roots = list;
            return _roots;
        }

        private static JArray FindChildren(JObject o)
        {
            foreach (var n in new string[] { "categories", "children", "subCategories", "sub", "list" })
            {
                var a = o[n] as JArray;
                if (a != null) return a;
            }
            return null;
        }

        private static CatNode Parse(JObject o)
        {
            var n = new CatNode();
            n.Id = (string)o["id"] ?? "";
            n.Title = ((string)o["title"] ?? (string)o["name"] ?? "").Trim();
            var kids = FindChildren(o);
            if (kids != null) foreach (var k in kids.OfType<JObject>()) n.Children.Add(Parse(k));
            return n;
        }
    }
}
