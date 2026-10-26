using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    public class DocRun
    {
        public string Text; public float Size; public string Tone;     // Tone: t=본문, m=보조, a=강조
        public DocRun(string text, float size, string tone) { Text = text; Size = size; Tone = tone; }
    }

    public class DocBlock
    {
        public List<DocRun> Runs = new List<DocRun>();
        public int Indent, Hang, Before, After;        // 96dpi 기준 픽셀
    }

    /// <summary>
    /// 사용 설명서 본문을 직접 그리는 읽기 전용 뷰어(줄바꿈, 스크롤, 검색어 강조, 전체 복사).
    /// RichTextBox 를 쓰지 않아 환경(글꼴, Windows/Mono)과 상관없이 같은 모양으로 나오고 테마를 그대로 따른다.
    /// </summary>
    public class DocView : FluentControl
    {
        private class Item { public int X, Y, W, H; public string T; public Font F; public Color C; public bool Hl; }
        private List<DocBlock> _blocks = new List<DocBlock>();
        private readonly List<Item> _items = new List<Item>();
        private readonly SoftScrollBar _sb = new SoftScrollBar();
        private int _total, _layoutW = -1;
        private string _hl = "", _plain = "";

        public DocView()
        {
            TabStop = true;
            _sb.Dock = DockStyle.Right; _sb.Scroll += delegate { Invalidate(); };
            Controls.Add(_sb);
            var cm = new ContextMenu();
            cm.MenuItems.Add("전체 복사", delegate { CopyAll(); });
            ContextMenu = cm;
        }

        public void SetContent(List<DocBlock> blocks, string plain, string highlight)
        {
            _blocks = blocks; _plain = plain ?? ""; _hl = (highlight ?? "").Trim(); _layoutW = -1;
            _sb.Value = 0; Relayout(); Invalidate();
        }

        public void CopyAll() { try { if (_plain.Length > 0) Clipboard.SetText(_plain); } catch { } }

        public int ContentHeight { get { return _total; } }
        public int ScrollPos { get { return _sb.Value; } }

        private int ViewW { get { return Math.Max(100, Width - _sb.Width - S(28)); } }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
        protected override void OnMouseWheel(MouseEventArgs e) { Scroll(-e.Delta / 120 * S(54)); base.OnMouseWheel(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.PageDown || k == Keys.PageUp || k == Keys.Home || k == Keys.End || k == Keys.Down || k == Keys.Up || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down: Scroll(S(40)); break; case Keys.Up: Scroll(-S(40)); break;
                case Keys.PageDown: Scroll(Height - S(40)); break; case Keys.PageUp: Scroll(-(Height - S(40))); break;
                case Keys.Home: Scroll(-_total); break; case Keys.End: Scroll(_total); break;
                case Keys.C: if (e.Control) CopyAll(); break;
            }
            base.OnKeyDown(e);
        }

        public void Scroll(int delta)
        {
            int max = Math.Max(0, _total - Height + S(20));
            _sb.Value = Math.Max(0, Math.Min(Math.Min(max, _sb.Maximum), _sb.Value + delta));
            Invalidate();
        }

        // ---------------------------------------------------------------- 배치
        private static readonly TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.Top;

        private static int Measure(string t, Font f)
        {
            if (t.Length == 0) return 0;
            // 끝의 공백도 폭에 넣기 위해 앞뒤에 기준 글자를 붙여 재고 뺀다
            int a = TextRenderer.MeasureText("|" + t + "|", f, new Size(int.MaxValue / 4, 200), Flags).Width;
            int b = TextRenderer.MeasureText("||", f, new Size(int.MaxValue / 4, 200), Flags).Width;
            return Math.Max(1, a - b);
        }

        private Color ToneColor(string tone) { var p = Theme.Current; return tone == "m" ? p.TextMuted : tone == "a" ? p.Accent : p.Text; }

        private void Relayout()
        {
            if (Width <= 0 || _blocks == null) return;
            int w = ViewW;
            _items.Clear();
            int left = S(14), y = S(10);
            foreach (var b in _blocks)
            {
                y += S(b.Before);
                int firstX = left + S(b.Indent - b.Hang), restX = left + S(b.Indent);
                int maxW = left + w;
                int x = firstX, lineTop = y, lineH = 0; bool first = true; int lineStartIdx = _items.Count;
                Action<int> newLine = null;
                newLine = h => { y = lineTop + lineH; lineTop = y; x = restX; lineH = 0; first = false; lineStartIdx = _items.Count; };
                foreach (var r in b.Runs)
                {
                    var f = Theme.Font(r.Size, FontStyle.Regular); var c = ToneColor(r.Tone);
                    int fh = f.Height + S(5);
                    if (fh > lineH) lineH = fh;
                    foreach (var tok in Tokens(r.Text))
                    {
                        bool space = tok.Trim().Length == 0;
                        int tw = Measure(tok, f);
                        if (x + tw > maxW && x > (first ? firstX : restX) && !space)
                        {
                            newLine(0); if (fh > lineH) lineH = fh;
                        }
                        if (space && x == (first ? firstX : restX) && !first) continue;   // 줄 맨 앞 공백 생략
                        if (!space && tw > maxW - x)
                        {
                            // 한 단어가 한 줄보다 길면 글자 단위로 자른다(긴 경로 등)
                            string rest = tok;
                            while (rest.Length > 0)
                            {
                                int n = rest.Length; while (n > 1 && x + Measure(rest.Substring(0, n), f) > maxW) n--;
                                string part = rest.Substring(0, n); Emit(part, x, lineTop, f, c, Measure(part, f), fh);
                                x += Measure(part, f); rest = rest.Substring(n);
                                if (rest.Length > 0) { newLine(0); if (fh > lineH) lineH = fh; }
                            }
                            continue;
                        }
                        Emit(tok, x, lineTop, f, c, tw, fh); x += tw;
                    }
                }
                y = lineTop + lineH + S(b.After);
            }
            _total = y + S(10);
            _layoutW = w;
            int visible = Height;
            _sb.Maximum = Math.Max(0, _total); _sb.LargeChange = Math.Max(1, visible); _sb.SmallChange = S(40);
            _sb.Enabled = _total > visible;
            if (_sb.Value > Math.Max(0, _total - visible)) _sb.Value = Math.Max(0, _total - visible);
        }

        private void Emit(string text, int x, int y, Font f, Color c, int w, int h)
        {
            if (_hl.Length == 0) { _items.Add(new Item { X = x, Y = y, W = w, H = h, T = text, F = f, C = c }); return; }
            int pos = 0, i, cx = x;
            while ((i = text.IndexOf(_hl, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                if (i > pos) { string a = text.Substring(pos, i - pos); int aw = Measure(a, f); _items.Add(new Item { X = cx, Y = y, W = aw, H = h, T = a, F = f, C = c }); cx += aw; }
                string m = text.Substring(i, _hl.Length); int mw = Measure(m, f);
                _items.Add(new Item { X = cx, Y = y, W = mw, H = h, T = m, F = f, C = c, Hl = true }); cx += mw; pos = i + _hl.Length;
            }
            if (pos < text.Length) { string a = text.Substring(pos); _items.Add(new Item { X = cx, Y = y, W = Measure(a, f), H = h, T = a, F = f, C = c }); }
        }

        /// <summary>공백/비공백 덩어리로 나눈다(한글은 띄어쓰기 단위로 줄바꿈).</summary>
        private static IEnumerable<string> Tokens(string t)
        {
            int i = 0;
            while (i < t.Length)
            {
                int j = i; bool sp = char.IsWhiteSpace(t[i]);
                while (j < t.Length && char.IsWhiteSpace(t[j]) == sp) j++;
                yield return t.Substring(i, j - i); i = j;
            }
        }

        // ---------------------------------------------------------------- 그리기
        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics; var p = Theme.Current;
            g.Clear(p.Card);
            int off = _sb.Value; Color hlBg = p.Dark ? Color.FromArgb(102, 92, 0) : Color.FromArgb(255, 241, 150);
            foreach (var it in _items)
            {
                int y = it.Y - off;
                if (y + it.H < 0 || y > Height) continue;
                if (it.Hl) using (var b = new SolidBrush(hlBg)) g.FillRectangle(b, it.X, y, it.W, it.H - S(3));
                TextRenderer.DrawText(g, it.T, it.F, new Point(it.X, y), it.C, Flags);
            }
        }

        public override void ApplyTheme() { BackColor = Theme.Current.Card; _layoutW = -1; Relayout(); }
    }
}
