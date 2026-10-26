using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>
    /// 상품 관리. 기존 DataGridView 의 모든 열/편집/CSV 동작을 그대로 두고, 검색·정렬·필터를 보기 전용으로 추가했다.
    /// 정렬/필터는 화면에만 적용되며 실제 목록(등록 순서)은 바뀌지 않는다.
    /// </summary>
    public class ProductsPage : PageBase
    {
        private readonly DataGridView grid;
        private readonly TextBox txtSearch;
        private readonly ComboBox cmbFilter;
        private readonly Label lblCount;
        private BindingList<Product> _view;
        private string _sortProp = ""; private bool _sortAsc = true;
        private bool _rebuilding;

        public ProductsPage(AppHost host) : base(host)
        {
            Key = "products"; PageTitle = "상품 관리";
            AutoScroll = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Theme.BgDark };
            var title = Ui.Title("상품 관리"); title.Location = new Point(0, 0);
            lblCount = Ui.Muted(""); lblCount.Location = new Point(2, 38);

            var bAdd = Ui.Button("상품 추가", "\uE710", BtnStyle.Primary);
            var bEdit = Ui.Button("편집", "\uE70F");
            var bDel = Ui.Button("삭제", "\uE74D");
            var bImport = Ui.Button("CSV 불러오기", "\uE896");
            var bExport = Ui.Button("CSV 저장", "\uE74E");
            var bTpl = Ui.Button("CSV 양식", "\uE8A5");
            var bImg = Ui.Button("사진 지정", "\uEB9F");
            var bReset = Ui.Button("상태 초기화", "\uE777");
            var bar = Ui.Row(bAdd, bEdit, bDel, bImport, bExport, bTpl, bImg, bReset); bar.Location = new Point(0, 70);
            bar.WrapContents = true; bar.MaximumSize = new Size(2000, 90);

            txtSearch = Ui.Input(260); txtSearch.Location = new Point(0, 114);
            cmbFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Location = new Point(272, 113), Font = Theme.FontBase() };
            cmbFilter.Items.AddRange(new object[] { "전체", "등록 대기", "등록 성공", "등록 실패" }); cmbFilter.SelectedIndex = 0;
            var hint = Ui.Muted("검색: 제목·카테고리·태그·설명·상태메시지"); hint.Location = new Point(414, 117);
            top.Controls.AddRange(new Control[] { title, lblCount, bar, txtSearch, cmbFilter, hint });

            grid = new DataGridView
            {
                Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersWidth = 28,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, AllowUserToResizeRows = false,
                MultiSelect = true
            };
            AddCol("Title", "제목", 200); AddCol("Price", "가격", 90); AddCol("Category", "카테고리 (대>중>소)", 160);
            AddCol("Condition", "상태", 110); AddCol("Quantity", "수량", 55); AddCol("Tags", "태그(쉼표)", 90);
            AddCol("Images", "사진 경로(;)", 130); AddCol("Description", "설명", 150);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "FreeShipping", HeaderText = "배송비포함", Width = 90 });
            AddCol("Status", "상태메시지", 170, true); AddCol("ProductId", "상품번호", 100, true); AddCol("LastPosted", "마지막 등록", 130, true);
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.Programmatic;

            var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            gridCard.Controls.Add(grid);
            Controls.Add(gridCard); Controls.Add(top);
            Theme.StyleGrid(grid);

            Host.Products.ListChanged += OnMasterChanged;
            Host.Auto.ProductsChanged += delegate { if (Visible) grid.Invalidate(); UpdateCount(); };
            txtSearch.TextChanged += delegate { Rebuild(); };
            cmbFilter.SelectedIndexChanged += delegate { Rebuild(); };
            grid.ColumnHeaderMouseClick += delegate (object s, DataGridViewCellMouseEventArgs e)
            {
                string prop = grid.Columns[e.ColumnIndex].DataPropertyName;
                if (_sortProp == prop) { if (_sortAsc) _sortAsc = false; else { _sortProp = ""; _sortAsc = true; } } else { _sortProp = prop; _sortAsc = true; }
                Rebuild(); UpdateSortGlyph();
            };
            grid.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
                string v = Convert.ToString(e.Value) ?? "";
                if (v == "등록됨") e.CellStyle.ForeColor = Theme.Success;
                else if (v.StartsWith("실패")) e.CellStyle.ForeColor = Theme.Danger;
                else if (v == "진행중") e.CellStyle.ForeColor = Theme.Accent;
            };

            bImport.Click += delegate { ImportCsv(); };
            bExport.Click += delegate { ExportCsv(); };
            bTpl.Click += delegate
            {
                using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "상품양식.csv" })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK) { CsvIO.WriteTemplate(d.FileName); Log.Write("양식 저장: " + d.FileName); }
            };
            bAdd.Click += delegate
            {
                var np = new Product();
                using (var f = new EditForm(np))
                    if (f.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        Host.Products.Add(np); Storage.SaveProducts(Host.Products);
                        Log.Write("INFO", "Products", "product.add", "상품을 추가했습니다. (전체 " + Host.Products.Count + "개)", "OK");
                    }
            };
            Action editSel = delegate
            {
                var p = Current(); if (p == null) return;
                using (var f = new EditForm(p))
                    if (f.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        grid.Refresh(); Storage.SaveProducts(Host.Products);
                        Log.Write("INFO", "Products", "product.edit", "상품을 수정했습니다.", "OK");
                    }
            };
            bEdit.Click += delegate { editSel(); };
            grid.RowHeaderMouseDoubleClick += delegate { editSel(); };
            bDel.Click += delegate
            {
                var sel = Selected(); if (sel.Count == 0) return;
                foreach (var p in sel) Host.Products.Remove(p);
                Storage.SaveProducts(Host.Products);
                Log.Write("INFO", "Products", "product.delete", "상품 " + sel.Count + "개를 삭제했습니다. (전체 " + Host.Products.Count + "개)", "OK");
            };
            bImg.Click += delegate
            {
                var p = Current(); if (p == null) return;
                using (var d = new OpenFileDialog { Multiselect = true, Filter = "이미지|*.jpg;*.jpeg;*.png;*.webp;*.gif|모든 파일|*.*" })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        p.Images = string.Join(";", d.FileNames.Take(12).Select(x => EditForm.CopyPhoto(x)).ToArray());
                        Storage.SaveProducts(Host.Products);
                        Log.Write("INFO", "Products", "product.images", "상품 사진을 지정했습니다. (" + Math.Min(12, d.FileNames.Length) + "장)", "OK");
                    }
            };
            bReset.Click += delegate
            {
                var sel = Selected();
                foreach (var p in sel) { p.Status = ""; p.ProductId = ""; p.LastPosted = ""; }
                Storage.SaveProducts(Host.Products);
                if (sel.Count > 0) Log.Write("INFO", "Products", "product.reset", "상품 " + sel.Count + "개의 등록 상태를 초기화했습니다.", "OK");
            };
            grid.CellEndEdit += delegate { Storage.SaveProducts(Host.Products); };
            grid.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            Rebuild();
        }

        private void AddCol(string prop, string header, int width, bool readOnly = false)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header, Width = width, ReadOnly = readOnly });
        }

        private Product Current() { return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as Product; }
        private List<Product> Selected() { return grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem as Product).Where(p => p != null).ToList(); }

        private void OnMasterChanged(object s, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemChanged) return;    // 값 변화는 같은 객체를 보고 있어 자동 반영
            if (IsHandleCreated) BeginInvoke((Action)Rebuild); else Rebuild();
        }

        private bool Filtering { get { return txtSearch.Text.Trim().Length > 0 || cmbFilter.SelectedIndex > 0 || _sortProp.Length > 0; } }

        private void Rebuild()
        {
            if (_rebuilding) return; _rebuilding = true;
            try
            {
                IEnumerable<Product> q = Host.Products;
                string s = txtSearch.Text.Trim();
                if (s.Length > 0) q = q.Where(p => Has(p.Title, s) || Has(p.Category, s) || Has(p.Tags, s) || Has(p.Description, s) || Has(p.Status, s) || Has(p.ProductId, s));
                switch (cmbFilter.SelectedIndex)
                {
                    case 1: q = q.Where(p => p.IsValid && !AppHost.IsDone(p) && !AppHost.IsFail(p)); break;
                    case 2: q = q.Where(AppHost.IsDone); break;
                    case 3: q = q.Where(p => !AppHost.IsDone(p) && AppHost.IsFail(p)); break;
                }
                if (_sortProp.Length > 0)
                {
                    Func<Product, object> key = SortKey(_sortProp);
                    q = _sortAsc ? q.OrderBy(key, new NaturalComparer()) : q.OrderByDescending(key, new NaturalComparer());
                }
                if (!Filtering)
                {
                    if (!object.ReferenceEquals(grid.DataSource, Host.Products)) grid.DataSource = Host.Products;   // 평소에는 실제 목록을 그대로 바인딩
                }
                else { _view = new BindingList<Product>(q.ToList()); grid.DataSource = _view; }
                UpdateCount();
            }
            finally { _rebuilding = false; }
        }

        private static bool Has(string a, string b) { return a != null && a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static Func<Product, object> SortKey(string prop)
        {
            switch (prop)
            {
                case "Price": return p => { long v; return long.TryParse(p.Price, out v) ? (object)v : 0L; };
                case "Quantity": return p => { long v; return long.TryParse(p.Quantity, out v) ? (object)v : 0L; };
                case "FreeShipping": return p => p.FreeShipping ? 1 : 0;
                default:
                    var pi = typeof(Product).GetProperty(prop);
                    return p => (object)(Convert.ToString(pi.GetValue(p, null)) ?? "");
            }
        }

        private class NaturalComparer : IComparer<object>
        {
            public int Compare(object a, object b)
            {
                if (a is long && b is long) return ((long)a).CompareTo((long)b);
                if (a is int && b is int) return ((int)a).CompareTo((int)b);
                return string.Compare(Convert.ToString(a), Convert.ToString(b), StringComparison.CurrentCultureIgnoreCase);
            }
        }

        private void UpdateSortGlyph()
        {
            foreach (DataGridViewColumn c in grid.Columns)
                c.HeaderCell.SortGlyphDirection = c.DataPropertyName == _sortProp ? (_sortAsc ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
        }

        private void UpdateCount()
        {
            int shown = grid.RowCount;
            lblCount.Text = Filtering ? "전체 " + Host.Products.Count + "개 중 " + shown + "개 표시 (화면 정렬/필터는 등록 순서에 영향을 주지 않습니다)" : "전체 " + Host.Products.Count + "개  ·  표의 칸을 직접 눌러 고칠 수 있습니다. 행 머리글을 두 번 누르면 편집 창이 열립니다.";
        }

        public override void OnActivated() { grid.Invalidate(); UpdateCount(); }
        public override void OnThemeChanged() { Theme.StyleGrid(grid); grid.Invalidate(); }

        // ============================ CSV (기존 MainForm 동작 그대로) ============================
        private void ImportCsv()
        {
            var owner = FindForm();
            using (var d = new OpenFileDialog { Filter = "CSV·텍스트 파일|*.csv;*.txt;*.tsv|모든 파일|*.*" })
            {
                if (d.ShowDialog(owner) != DialogResult.OK) return;
                try
                {
                    var items = CsvIO.Read(d.FileName);
                    bool replace = Host.Products.Count == 0 ||
                        FluentBox.Show(owner, "기존 목록을 지우고 새로 불러올까요?\r\n(아니오: 아래에 이어서 추가)", "CSV 불러오기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                    if (replace) Host.Products.Clear();
                    foreach (var p in items) Host.Products.Add(p);
                    Host.Cfg.LastCsv = d.FileName;
                    Storage.SaveProducts(Host.Products);
                    Log.Write("CSV " + items.Count + "건을 불러왔습니다: " + d.FileName);
                    int bad = items.Count(x => (x.Description ?? "").Contains("??") || (x.Title ?? "").Contains("??"));
                    if (bad > 0)
                        Log.Write("[주의] 제목/설명에 '??' 가 있는 상품이 " + bad + "건 있습니다. 엑셀에서 'CSV(쉼표로 분리)'로 저장하면 이모지·특수기호가 '?'로 바뀌어 복구되지 않습니다. " +
                                  "원본 엑셀에서 'CSV UTF-8(쉼표로 분리)'로 다시 저장해 불러오세요.");
                }
                catch (Exception ex) { Log.Write("WARN", "Csv", "csv.import", "CSV 불러오기 실패: " + ex.Message, "FAIL"); FluentBox.Show(owner, "불러오기 실패: " + ex.Message, "CSV 불러오기", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void ExportCsv()
        {
            var owner = FindForm();
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "상품목록.csv" })
            {
                if (d.ShowDialog(owner) != DialogResult.OK) return;
                try { CsvIO.Write(d.FileName, Host.Products); Log.Write("CSV 저장: " + d.FileName); }
                catch (Exception ex) { Log.Write("WARN", "Csv", "csv.export", "CSV 저장 실패: " + ex.Message, "FAIL"); FluentBox.Show(owner, "저장 실패: " + ex.Message + (ex is IOException ? "\r\n\r\n엑셀 등에서 같은 파일을 열어 두었다면 닫고 다시 저장해 주세요." : ""), "오류"); }
            }
        }
    }
}
