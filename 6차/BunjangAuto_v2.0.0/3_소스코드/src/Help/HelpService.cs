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

        // RichTextBox 의 선택 영역(SelectionFont/SelectionColor)에 서식을 지정하며 한 조각씩 이어 붙인다.
        public static void Render(RichTextBox rtb, HelpTopic t, string highlight)
        {
            var p = Theme.Current;
            rtb.SuspendLayout();
            rtb.Clear();
            rtb.BackColor = p.Card; rtb.ForeColor = p.Text;
            if (t == null) { rtb.ResumeLayout(); return; }

            string[] lines = t.Markdown.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length == 0) { Append(rtb, "\n", Theme.Font(5f, FontStyle.Regular), p.Text); continue; }
                if (line.StartsWith("# ")) { Append(rtb, line.Substring(2) + "\n", Theme.Font(20f, FontStyle.Bold), p.Text); continue; }
                if (line.StartsWith("## ")) { Append(rtb, "\n", Theme.Font(4f, FontStyle.Regular), p.Text); Append(rtb, line.Substring(3) + "\n", Theme.Font(13f, FontStyle.Bold), p.Accent); continue; }
                if (line.StartsWith("> ")) { Append(rtb, "  \u258C ", Theme.Font(10.5f, FontStyle.Bold), p.Accent); AppendInline(rtb, line.Substring(2), p.TextMuted, 10.5f); Append(rtb, "\n", Theme.FontBase(), p.Text); continue; }
                if (line.StartsWith("- ")) { Append(rtb, "   \u2022  ", Theme.Font(10.5f, FontStyle.Regular), p.Accent); AppendInline(rtb, line.Substring(2), p.Text, 10.5f); Append(rtb, "\n", Theme.FontBase(), p.Text); continue; }
                var num = Regex.Match(line, @"^(\d+)\.\s+(.*)$");
                if (num.Success) { Append(rtb, "   " + num.Groups[1].Value + ".  ", Theme.Font(10.5f, FontStyle.Bold), p.Accent); AppendInline(rtb, num.Groups[2].Value, p.Text, 10.5f); Append(rtb, "\n", Theme.FontBase(), p.Text); continue; }
                if (line.StartsWith("|"))
                {
                    var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                    if (cells.All(c => Regex.IsMatch(c, @"^:?-{2,}:?$"))) continue;                                  // 구분선
                    if (i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\|[\s:\-|]+\|\s*$")) continue;           // 표 머리글 줄(각 행에서 설명)
                    Append(rtb, "   \u25B8  ", Theme.Font(10.5f, FontStyle.Regular), p.Accent);
                    Append(rtb, cells[0], Theme.Font(10.5f, FontStyle.Bold), p.Text);
                    if (cells.Length > 1) { Append(rtb, "   \u2014   ", Theme.FontBase(), p.TextMuted); AppendInline(rtb, string.Join("   \u00B7   ", cells.Skip(1).ToArray()), p.Text, 10.5f); }
                    Append(rtb, "\n", Theme.FontBase(), p.Text);
                    continue;
                }
                AppendInline(rtb, line, p.Text, 10.5f);
                Append(rtb, "\n", Theme.FontBase(), p.Text);
            }

            if (!string.IsNullOrWhiteSpace(highlight))
            {
                string text = rtb.Text; int idx = 0; string h = highlight.Trim();
                Color bg = p.Dark ? Color.FromArgb(102, 92, 0) : Color.FromArgb(255, 241, 150);
                while ((idx = text.IndexOf(h, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    rtb.Select(idx, h.Length); rtb.SelectionBackColor = bg;
                    idx += Math.Max(1, h.Length);
                }
            }
            rtb.Select(0, 0);
            try { rtb.ScrollToCaret(); } catch { }
            rtb.ResumeLayout();
        }

        private static void Append(RichTextBox rtb, string text, Font f, Color c)
        {
            rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
            rtb.SelectionFont = f; rtb.SelectionColor = c; rtb.SelectionBackColor = rtb.BackColor;
            rtb.SelectedText = text;
        }

        private static void AppendInline(RichTextBox rtb, string text, Color c, float size)
        {
            int pos = 0;
            foreach (Match m in Inline.Matches(text))
            {
                if (m.Index > pos) Append(rtb, text.Substring(pos, m.Index - pos), Theme.Font(size, FontStyle.Regular), c);
                string tok = m.Value;
                if (tok.StartsWith("**")) Append(rtb, tok.Substring(2, tok.Length - 4), Theme.Font(size, FontStyle.Bold), Theme.Current.Text);
                else Append(rtb, tok.Substring(1, tok.Length - 2), Theme.FontMono(size - 0.5f), Theme.Current.Accent);
                pos = m.Index + m.Length;
            }
            if (pos < text.Length) Append(rtb, text.Substring(pos), Theme.Font(size, FontStyle.Regular), c);
        }
    }
}
