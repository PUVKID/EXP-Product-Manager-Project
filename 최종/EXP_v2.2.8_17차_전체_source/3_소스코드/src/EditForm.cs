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
        private ShippingEditor shipping;
        private ListBox lstImg;

        public EditForm(Product p)
        {
            _p = p;
            Text = "EXP · 상품 편집"; ClientSize=new Size(1020,1102);
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;Font=Theme.FontBase();
            Controls.Add(new HeroBanner {Left=24,Top=20,Width=972,Height=136,Eyebrow="EXP / PRODUCT",Title="상품을 더 매력적으로.",Subtitle="정보와 사진을 준비하고 저장하세요."});
            var left=new SoftCard {Left=24,Top=174,Width=472,Height=548};
            var right=new SoftCard {Left=512,Top=174,Width=484,Height=548};
            left.Controls.Add(Design.Label("기본 정보",24,20,420,28,15,"t"));
            txtTitle=Field(left,"상품명",24,64,424,p.Title);txtTitle.Name="productTitle";
            txtPrice=Field(left,"가격 · 원",24,150,194,p.Price);txtPrice.Name="productPrice";
            txtQty=Field(left,"수량",248,150,200,string.IsNullOrEmpty(p.Quantity)?"1":p.Quantity);
            left.Controls.Add(Design.Label("카테고리 · 대분류 → 중분류 → 소분류",24,234,424,24,10,"m"));
            _roots=CategoryStore.Roots(false);
            cat1=Category(24,264,424);cat2=Category(24,304,424);cat3=Category(24,344,424);
            left.Controls.AddRange(new Control[]{cat1,cat2,cat3});
            var parts=(p.Category??"").Split('>').Select(x=>x.Trim()).ToArray();
            cat1.Items.AddRange(_roots.Select(r=>(object)r.Title).ToArray());cat1.Text=parts.Length>0?parts[0]:"";
            FillChild(cat2,cat1.Text,null);cat2.Text=parts.Length>1?parts[1]:"";
            FillChild(cat3,cat1.Text,cat2.Text);cat3.Text=parts.Length>2?parts[2]:"";
            cat1.SelectedIndexChanged+=delegate{cat2.Text="";cat3.Text="";FillChild(cat2,cat1.Text,null);cat3.Items.Clear();};
            cat2.SelectedIndexChanged+=delegate{cat3.Text="";FillChild(cat3,cat1.Text,cat2.Text);};
            left.Controls.Add(Design.Label("상품 상태",24,392,238,24,10,"m"));
            cmbCond=Category(24,420,238);cmbCond.Items.AddRange(new object[]{"새 상품(미사용)","사용감 없음","사용감 적음","사용감 많음","고장/파손 상품"});cmbCond.Text=p.Condition;
            left.Controls.Add(cmbCond);
            txtTags=Field(left,"태그 · 쉼표 또는 띄어쓰기로 구분",24,464,424,p.Tags);
            right.Controls.Add(Design.Label("상품 설명",24,20,430,28,15,"t"));
            var description=new FieldFrame {Left=24,Top=62,Width=436,Height=224,Padding=new Padding(12)};
            txtDesc=new TextBox {Name="productDescription",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,Font=Theme.Font(11f,FontStyle.Bold),Text=(p.Description??"").Replace("\r\n","\n").Replace("\n","\r\n")};description.Controls.Add(txtDesc);right.Controls.Add(description);
            right.Controls.Add(Design.Label("사진",24,310,230,28,15,"t"));
            var count=Design.Label("최대 12장",320,314,140,24,10,"m");right.Controls.Add(count);
            lstImg=new ListBox {Name="productPhotos",Left=24,Top=348,Width=436,Height=132,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Dpi.S(44),SelectionMode=SelectionMode.MultiExtended,IntegralHeight=false};
            foreach(var photo in(p.Images??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))lstImg.Items.Add(photo.Trim());
            lstImg.DrawItem+=DrawPhoto;right.Controls.Add(lstImg);
            var bAddImg=Design.Button("사진 추가",24,494,118,true);var bDelImg=Design.Button("삭제",150,494,94,false);var bUp=Design.Button("위로",252,494,94,false);var bDown=Design.Button("아래로",354,494,106,false);
            right.Controls.AddRange(new Control[]{bAddImg,bDelImg,bUp,bDown});
            shipping=new ShippingEditor(p){Left=24,Top=742};Controls.Add(shipping);
            var ok=Design.Button("상품 저장",810,1040,186,true);ok.Name="saveProduct";ok.Height=42;ok.DialogResult=DialogResult.OK;
            var cancel=Design.Button("취소",680,1040,118,false);cancel.Height=42;cancel.DialogResult=DialogResult.Cancel;
            Controls.AddRange(new Control[]{left,right,ok,cancel});CancelButton=cancel;AcceptButton=null;
            Design.Scale(this);Theme.Apply(this);

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
                try { shipping.ValidateValues(); } catch(Exception ex){FluentBox.Show(this,ex.Message,"배송 설정");DialogResult=DialogResult.None;return;}
                _p.Title = txtTitle.Text.Trim();
                _p.Price = new string(txtPrice.Text.Where(char.IsDigit).ToArray());
                _p.Category = string.Join(">", new string[] { cat1.Text.Trim(), cat2.Text.Trim(), cat3.Text.Trim() }.Where(x => x.Length > 0).ToArray());
                _p.Condition = cmbCond.Text.Trim();
                _p.Quantity = string.IsNullOrWhiteSpace(txtQty.Text) ? "1" : txtQty.Text.Trim();
                _p.Tags = txtTags.Text.Trim();
                shipping.Save(_p);
                _p.Description = txtDesc.Text;
                _p.Images = string.Join(";", lstImg.Items.Cast<object>().Select(x => x.ToString()).ToArray());
            };
        }

        readonly Dictionary<string,Image> thumbnails=new Dictionary<string,Image>();
        static ComboBox Category(int x,int y,int width) {return new SoftCombo {Left=x,Top=y,Width=width,DropDownStyle=ComboBoxStyle.DropDown};}
        static TextBox Field(Control parent,string title,int x,int y,int width,string value) {
            parent.Controls.Add(Design.Label(title,x,y,width,24,10,"m"));
            var frame=new FieldFrame {Left=x,Top=y+28,Width=width,Height=42};
            var input=new TextBox {Text=value??"",Left=12,Top=11,Width=width-24,BorderStyle=BorderStyle.None,Font=Theme.Font(11f,FontStyle.Bold),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};
            frame.Controls.Add(input);parent.Controls.Add(frame);return input;
        }
        void DrawPhoto(object sender,DrawItemEventArgs e) {
            var pal=Theme.Current;bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?pal.Selected:pal.CardAlt))e.Graphics.FillRectangle(brush,e.Bounds);
            if(e.Index<0)return;string path=Convert.ToString(lstImg.Items[e.Index]);Image thumb;
            if(!thumbnails.TryGetValue(path,out thumb)) {try{using(var original=Image.FromFile(path)){thumb=new Bitmap(64,64);using(var g=Graphics.FromImage(thumb)){g.Clear(pal.CardAlt);double scale=Math.Min(64.0/original.Width,64.0/original.Height);int w=(int)(original.Width*scale),h=(int)(original.Height*scale);g.DrawImage(original,(64-w)/2,(64-h)/2,w,h);}}}catch{thumb=null;}thumbnails[path]=thumb;}
            int box=Dpi.S(34);if(thumb!=null)e.Graphics.DrawImage(thumb,e.Bounds.X+Dpi.S(6),e.Bounds.Y+Dpi.S(5),box,box);
            TextRenderer.DrawText(e.Graphics,(e.Index+1)+"  "+Path.GetFileName(path),Theme.FontBase(),new Rectangle(e.Bounds.X+Dpi.S(50),e.Bounds.Y,e.Bounds.Width-Dpi.S(56),e.Bounds.Height),pal.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(selected)e.DrawFocusRectangle();
        }
        protected override void Dispose(bool disposing) {if(disposing)foreach(var image in thumbnails.Values)if(image!=null)image.Dispose();base.Dispose(disposing);}

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
