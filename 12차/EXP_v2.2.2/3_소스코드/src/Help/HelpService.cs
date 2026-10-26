using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BunjangAuto
{
    public class HelpTopic
    {
        public string Id = "";
        public string Title = "";
        public string Markdown = "";
    }

    /// <summary>
    /// 사용 설명서. 문서는 빌드할 때 exe 안에 리소스(help.*.md)로 내장되므로 인터넷·외부 프로그램 없이 열린다.
    /// 간단한 마크다운(제목, 굵게, `코드`, 목록, 인용, 표)을 RichTextBox 로 직접 그린다.
    /// </summary>
    public static class HelpService
    {
        private static List<HelpTopic> _topics;

        public static List<HelpTopic> Topics()
        {
            if (_topics != null) return _topics;
            var list = new List<HelpTopic>();
            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames().Where(n => n.StartsWith("help.") && n.EndsWith(".md")).OrderBy(n => n, StringComparer.Ordinal).ToList();
            foreach (var n in names)
            {
                using (var s = asm.GetManifestResourceStream(n))
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    string md = r.ReadToEnd().Replace("\r\n", "\n");
                    var t = new HelpTopic { Id = n, Markdown = md, Title = n };
                    var m = Regex.Match(md, @"^#\s+(.+)$", RegexOptions.Multiline);
                    if (m.Success) t.Title = m.Groups[1].Value.Trim();
                    list.Add(t);
                }
            }
            _topics = list;
            return list;
        }

        /// <summary>제목이나 본문에 검색어가 들어 있는 문서(대소문자 무시). 제목에 있는 문서를 앞에 둔다.</summary>
        public static List<HelpTopic> Search(string q)
        {
            var all = Topics();
            if (string.IsNullOrWhiteSpace(q)) return all;
            q = q.Trim();
            var hits = all.Where(t => t.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || t.Markdown.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            return hits.OrderBy(t => t.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
        }

        private static readonly Regex Inline = new Regex(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

        /// <summary>마크다운 한 문서를 DocView 가 그릴 블록 목록으로 바꾼다.</summary>
        public static List<DocBlock> Build(HelpTopic t)
        {
            var list = new List<DocBlock>();
            if (t == null) return list;
            string[] lines = t.Markdown.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length == 0) continue;
                var b = new DocBlock();
                if (line.StartsWith("# ")) { b.After = 10; b.Runs.Add(new DocRun(line.Substring(2), 20f, "t")); }
                else if (line.StartsWith("## ")) { b.Before = 18; b.After = 6; b.Runs.Add(new DocRun(line.Substring(3), 13f, "a")); }
                else if (line.StartsWith("> ")) { b.Indent = 26; b.Hang = 16; b.Before = 2; b.After = 6; b.Runs.Add(new DocRun("\u258C ", 10.5f, "a")); Inl(b, line.Substring(2), 10.5f, "m"); }
                else if (line.StartsWith("- ")) { b.Indent = 34; b.Hang = 18; b.After = 4; b.Runs.Add(new DocRun("\u2022 ", 10.5f, "a")); Inl(b, line.Substring(2), 10.5f, "t"); }
                else
                {
                    var num = Regex.Match(line, @"^(\d+)\.\s+(.*)$");
                    if (num.Success) { b.Indent = 36; b.Hang = 22; b.After = 5; b.Runs.Add(new DocRun(num.Groups[1].Value + ". ", 10.5f, "a")); Inl(b, num.Groups[2].Value, 10.5f, "t"); }
                    else if (line.StartsWith("|"))
                    {
                        var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                        if (cells.All(c => Regex.IsMatch(c, @"^:?-{2,}:?$"))) continue;                                  // 구분선
                        if (i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\|[\s:\-|]+\|\s*$")) continue;           // 머리글 줄(각 행에서 설명)
                        b.Indent = 34; b.Hang = 18; b.After = 5;
                        b.Runs.Add(new DocRun("\u25B8 ", 10.5f, "a"));
                        b.Runs.Add(new DocRun(cells[0], 10.5f, "t"));
                        if (cells.Length > 1) { b.Runs.Add(new DocRun("  \u2014  ", 10.5f, "m")); Inl(b, string.Join("  |  ", cells.Skip(1).ToArray()), 10.5f, "t"); }
                    }
                    else { b.After = 7; Inl(b, line, 10.5f, "t"); }
                }
                list.Add(b);
            }
            return list;
        }

        private static void Inl(DocBlock b, string text, float size, string tone)
        {
            int pos = 0;
            foreach (Match m in Inline.Matches(text))
            {
                if (m.Index > pos) b.Runs.Add(new DocRun(text.Substring(pos, m.Index - pos), size, tone));
                string tok = m.Value;
                if (tok.StartsWith("**")) b.Runs.Add(new DocRun(tok.Substring(2, tok.Length - 4), size, "t"));
                else b.Runs.Add(new DocRun(tok.Substring(1, tok.Length - 2), size - 0.5f, "a"));
                pos = m.Index + m.Length;
            }
            if (pos < text.Length) b.Runs.Add(new DocRun(text.Substring(pos), size, tone));
        }

        /// <summary>문서의 평문(복사용)</summary>
        public static string PlainText(HelpTopic t)
        {
            var sb = new StringBuilder();
            foreach (var b in Build(t)) { foreach (var r in b.Runs) sb.Append(r.Text); sb.AppendLine(); }
            return sb.ToString();
        }
    }
}
