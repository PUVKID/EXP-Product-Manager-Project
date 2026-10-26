using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>CSV 없이 상품을 직접 입력/수정하는 창. 사진은 앱 데이터 폴더로 복사해 원본이 사라져도 등록된다.</summary>
    public class EditForm : Form
    {
        private readonly Product _p;
        private TextBox txtTitle, txtPrice, txtQty, txtTags, txtDesc;
        private ComboBox cat1, cat2, cat3;
        private List<CatNode> _roots;
        private ComboBox cmbCond;
        private CheckBox chkShip;
        private ListBox lstImg;

        public EditForm(Product p)
        {
            _p = p;
            Text = "상품 입력";
            Width = 640; Height = 760;
            StartPosition = FormStartPosition.CenterParent;
            Font = Theme.FontBase();

            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtTitle = new TextBox { Dock = DockStyle.Fill, Text = p.Title };
            txtPrice = new TextBox { Width = 120, Text = p.Price };
            _roots = CategoryStore.Roots(false);
            cat1 = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDown };
            cat2 = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDown };
            cat3 = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDown };
            var catPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            catPanel.Controls.AddRange(new Control[] { cat1, cat2, cat3 });
            var parts = (p.Category ?? "").Split('>').Select(x => x.Trim()).ToArray();
            cat1.Items.AddRange(_roots.Select(r => (object)r.Title).ToArray());
            cat1.Text = parts.Length > 0 ? parts[0] : "";
            FillChild(cat2, cat1.Text, null);
            cat2.Text = parts.Length > 1 ? parts[1] : "";
            FillChild(cat3, cat1.Text, cat2.Text);
            cat3.Text = parts.Length > 2 ? parts[2] : "";
            cat1.SelectedIndexChanged += delegate { cat2.Text = ""; cat3.Text = ""; FillChild(cat2, cat1.Text, null); cat3.Items.Clear(); };
            cat2.SelectedIndexChanged += delegate { cat3.Text = ""; FillChild(cat3, cat1.Text, cat2.Text); };
            cmbCond = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
            cmbCond.Items.AddRange(new object[] { "새 상품(미사용)", "사용감 없음", "사용감 적음", "사용감 많음", "고장/파손 상품" });
            cmbCond.Text = p.Condition;
            txtQty = new TextBox { Width = 60, Text = string.IsNullOrEmpty(p.Quantity) ? "1" : p.Quantity };
            txtTags = new TextBox { Dock = DockStyle.Fill, Text = p.Tags };
            chkShip = new CheckBox { Text = "배송비 포함", AutoSize = true, Checked = p.FreeShipping };
            txtDesc = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Height = 200, Text = (p.Description ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n") };

            lstImg = new ListBox { Dock = DockStyle.Fill, Height = 110, SelectionMode = SelectionMode.MultiExtended };
            foreach (var f in (p.Images ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) lstImg.Items.Add(f.Trim());
            var bAddImg = new Button { Text = "사진 추가", AutoSize = true };
            var bDelImg = new Button { Text = "선택 삭제", AutoSize = true };
            var bUp = new Button { Text = "위로", AutoSize = true };
            var bDown = new Button { Text = "아래로", AutoSize = true };
            var imgBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            imgBar.Controls.AddRange(new Control[] { bAddImg, bDelImg, bUp, bDown });
            var imgPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
            imgPanel.Controls.Add(lstImg); imgPanel.Controls.Add(imgBar);

            Row(tl, "제목", txtTitle);
            Row(tl, "가격(원)", txtPrice);
            Row(tl, "카테고리 (대/중/소)", catPanel);
            Row(tl, "상품상태", cmbCond);
            Row(tl, "수량", txtQty);
            Row(tl, "태그 (띄어쓰기/쉼표)", txtTags);
            Row(tl, "", chkShip);
            Row(tl, "설명", txtDesc);
            Row(tl, "사진 (최대 12장, jpg/png)", imgPanel);

            var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Width = 100, Height = 32 };
            var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Width = 100, Height = 32 };
            var bot = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8) };
            bot.Controls.Add(cancel); bot.Controls.Add(ok);
            var header = Theme.BuildHeader("상품 입력", "번개장터에 등록할 상품 정보를 입력하세요");
            Controls.Add(tl); Controls.Add(bot); Controls.Add(header);
            AcceptButton = null; CancelButton = cancel;
            Theme.MarkPrimary(ok);
            Ui.ScaleForm(this);
            Theme.Apply(this);

            bAddImg.Click += delegate
            {
                using (var d = new OpenFileDialog { Multiselect = true, Filter = "사진|*.jpg;*.jpeg;*.png|모든 파일|*.*" })
                    if (d.ShowDialog(this) == DialogResult.OK)
                        foreach (var f in d.FileNames)
                        {
                            if (lstImg.Items.Count >= 12) break;
                            lstImg.Items.Add(CopyPhoto(f));
                        }
            };
            bDelImg.Click += delegate { foreach (var o in lstImg.SelectedItems.Cast<object>().ToList()) lstImg.Items.Remove(o); };
            bUp.Click += delegate { MoveImg(-1); };
            bDown.Click += delegate { MoveImg(1); };

            ok.Click += delegate (object s, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtPrice.Text))
                {
                    FluentBox.Show(this, "제목과 가격은 꼭 입력해 주세요.", "안내");
                    DialogResult = DialogResult.None;
                    return;
                }
                _p.Title = txtTitle.Text.Trim();
                _p.Price = new string(txtPrice.Text.Where(char.IsDigit).ToArray());
                _p.Category = string.Join(">", new string[] { cat1.Text.Trim(), cat2.Text.Trim(), cat3.Text.Trim() }.Where(x => x.Length > 0).ToArray());
                _p.Condition = cmbCond.Text.Trim();
                _p.Quantity = string.IsNullOrWhiteSpace(txtQty.Text) ? "1" : txtQty.Text.Trim();
                _p.Tags = txtTags.Text.Trim();
                _p.FreeShipping = chkShip.Checked;
                _p.Description = txtDesc.Text;
                _p.Images = string.Join(";", lstImg.Items.Cast<object>().Select(x => x.ToString()).ToArray());
            };
        }

        private void FillChild(ComboBox cb, string l1, string l2)
        {
            cb.Items.Clear();
            var a = _roots.FirstOrDefault(r => r.Title == l1);
            if (a == null) return;
            var list = a.Children;
            if (l2 != null)
            {
                var b = a.Children.FirstOrDefault(r => r.Title == l2);
                if (b == null) return;
                list = b.Children;
            }
            cb.Items.AddRange(list.Select(r => (object)r.Title).ToArray());
        }

        private void MoveImg(int dir)
        {
            int i = lstImg.SelectedIndex; int j = i + dir;
            if (i < 0 || j < 0 || j >= lstImg.Items.Count) return;
            var o = lstImg.Items[i]; lstImg.Items.RemoveAt(i); lstImg.Items.Insert(j, o); lstImg.SelectedIndex = j;
        }

        private static void Row(TableLayoutPanel tl, string label, Control c)
        {
            int r = tl.RowCount++;
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 6) }, 0, r);
            c.Margin = new Padding(3, 4, 3, 4);
            tl.Controls.Add(c, 1, r);
        }

        /// <summary>사진을 앱 데이터 폴더(photos)로 복사하고 새 경로를 돌려준다. 이미 그 폴더에 있으면 그대로.</summary>
        public static string CopyPhoto(string src)
        {
            try
            {
                string dir = Path.Combine(Paths.Root, "photos");
                Directory.CreateDirectory(dir);
                if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(src)), dir, StringComparison.OrdinalIgnoreCase)) return src;
                string dst = Path.Combine(dir, DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + Path.GetFileName(src));
                File.Copy(src, dst, true);
                return dst;
            }
            catch { return src; }
        }
    }
}
