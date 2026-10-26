using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>EXE 안에 내장된 사용 설명서: 왼쪽 목차 + 오른쪽 본문 + 검색. 인터넷·외부 프로그램이 필요 없다.</summary>
    public class HelpPage : PageBase
    {
        private readonly TextBox txtSearch;
        private readonly FlatList lstToc;
        private readonly DocView doc;
        private readonly Label lblHit;
        private List<HelpTopic> _shown = new List<HelpTopic>();

        public HelpPage(AppHost host) : base(host)
        {
            Key = "help"; PageTitle = "사용 설명서";
            AutoScroll = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.BgDark };
            var title = Ui.Title("사용 설명서"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("인터넷 없이도 볼 수 있습니다."); sub.Location = new Point(2, 38);
            top.Controls.AddRange(new Control[] { title, sub });

            var left = new CardPanel { Dock = DockStyle.Left, Width = 290, Padding = new Padding(10) };
            txtSearch = Ui.Input(260); txtSearch.Location = new Point(12, 12);
            lblHit = Ui.Muted(""); lblHit.Location = new Point(14, 46);
            lstToc = new FlatList { Location = new Point(8, 70), Size = new Size(272, 400), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(34) };
            lstToc.DrawItem += DrawToc;
            left.Controls.AddRange(new Control[] { txtSearch, lblHit, lstToc });
            left.Resize += delegate
            {
                txtSearch.Width = left.Width - Ui.S(24); lblHit.Width = left.Width - Ui.S(28);
                lstToc.Width = left.Width - Ui.S(16); lstToc.Height = Math.Max(Ui.S(100), left.Height - Ui.S(80));
            };

            var gap = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Theme.BgDark };
            var right = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            doc = new DocView { Dock = DockStyle.Fill };
            right.Controls.Add(doc);

            Controls.Add(right); Controls.Add(gap); Controls.Add(left); Controls.Add(top);
            txtSearch.TextChanged += delegate { Filter(); };
            lstToc.SelectedIndexChanged += delegate { ShowSelected(); };
            Filter();
        }

        private void DrawToc(object sender, DrawItemEventArgs e)
        {
            var p = Theme.Current;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(p.Card)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0 || e.Index >= _shown.Count) return;
            var r = new Rectangle(e.Bounds.X + Ui.S(4), e.Bounds.Y + Ui.S(2), e.Bounds.Width - Ui.S(8), e.Bounds.Height - Ui.S(4));
            if (sel) { using (var path = Theme.RoundedRect(r, Ui.S(5))) using (var b = new SolidBrush(p.Selected)) e.Graphics.FillPath(b, path); using (var b = new SolidBrush(p.Accent)) e.Graphics.FillRectangle(b, r.X, r.Y + Ui.S(8), Ui.S(3), r.Height - Ui.S(16)); }
            TextRenderer.DrawText(e.Graphics, _shown[e.Index].Title, sel ? Theme.Font(9.5f, FontStyle.Bold) : Theme.FontBase(), new Rectangle(r.X + Ui.S(12), r.Y, r.Width - Ui.S(14), r.Height), p.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private void Filter()
        {
            string q = txtSearch.Text.Trim();
            _shown = HelpService.Search(q);
            lstToc.BeginUpdate(); lstToc.Items.Clear(); foreach (var t in _shown) lstToc.Items.Add(t.Title); lstToc.EndUpdate();
            lblHit.Text = q.Length == 0 ? _shown.Count + "개 문서" : "'" + q + "' 검색 결과 " + _shown.Count + "개";
            if (_shown.Count > 0) lstToc.SelectedIndex = 0;
            else doc.SetContent(new List<DocBlock> { NoResult() }, "", "");
        }

        private static DocBlock NoResult() { var b = new DocBlock(); b.Runs.Add(new DocRun("검색 결과가 없습니다.", 10.5f, "m")); return b; }

        private void ShowSelected()
        {
            int i = lstToc.SelectedIndex;
            if (i < 0 || i >= _shown.Count) return;
            var t = _shown[i];
            doc.SetContent(HelpService.Build(t), HelpService.PlainText(t), txtSearch.Text);
        }

        /// <summary>다른 화면(오류 안내 등)에서 특정 문서를 바로 열 때 사용</summary>
        public void Open(string titleContains)
        {
            txtSearch.Text = "";
            for (int i = 0; i < _shown.Count; i++) if (_shown[i].Title.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0) { lstToc.SelectedIndex = i; return; }
        }

        public override void OnActivated() { ShowSelected(); }
        public override void OnThemeChanged() { lstToc.Invalidate(); ShowSelected(); }
    }
}
