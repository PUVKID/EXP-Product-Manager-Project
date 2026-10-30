const fs=require('fs'),p=require('path'),root=p.resolve('work/round11b/source');
const f=p.join(root,'src/EditForm.cs');let s=fs.readFileSync(f,'utf8');
const start=s.indexOf('            Text = "상품 입력";'),end=s.indexOf('            bAddImg.Click += delegate',start);
s=s.slice(0,start)+`            Text = "EXP · 상품 편집"; ClientSize=new Size(1020,804);
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
            chkShip=new CheckBox {Left=282,Top=423,Width=162,Height=28,Text="배송비 포함",Checked=p.FreeShipping};left.Controls.AddRange(new Control[]{cmbCond,chkShip});
            txtTags=Field(left,"태그 · 쉼표 또는 띄어쓰기로 구분",24,464,424,p.Tags);
            right.Controls.Add(Design.Label("상품 설명",24,20,430,28,15,"t"));
            var description=new FieldFrame {Left=24,Top=62,Width=436,Height=224,Padding=new Padding(12)};
            txtDesc=new TextBox {Name="productDescription",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,Font=Theme.Font(11f,FontStyle.Bold),Text=(p.Description??"").Replace("\\r\\n","\\n").Replace("\\n","\\r\\n")};description.Controls.Add(txtDesc);right.Controls.Add(description);
            right.Controls.Add(Design.Label("사진",24,310,230,28,15,"t"));
            var count=Design.Label("최대 12장",320,314,140,24,10,"m");right.Controls.Add(count);
            lstImg=new ListBox {Name="productPhotos",Left=24,Top=348,Width=436,Height=132,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=Dpi.S(44),SelectionMode=SelectionMode.MultiExtended,IntegralHeight=false};
            foreach(var photo in(p.Images??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))lstImg.Items.Add(photo.Trim());
            lstImg.DrawItem+=DrawPhoto;right.Controls.Add(lstImg);
            var bAddImg=Design.Button("사진 추가",24,494,118,true);var bDelImg=Design.Button("삭제",150,494,94,false);var bUp=Design.Button("위로",252,494,94,false);var bDown=Design.Button("아래로",354,494,106,false);
            right.Controls.AddRange(new Control[]{bAddImg,bDelImg,bUp,bDown});
            var ok=Design.Button("상품 저장",810,742,186,true);ok.Name="saveProduct";ok.Height=42;ok.DialogResult=DialogResult.OK;
            var cancel=Design.Button("취소",680,742,118,false);cancel.Height=42;cancel.DialogResult=DialogResult.Cancel;
            Controls.AddRange(new Control[]{left,right,ok,cancel});CancelButton=cancel;AcceptButton=null;
            Design.Scale(this);Theme.Apply(this);

`+s.slice(end);
const methods=`        readonly Dictionary<string,Image> thumbnails=new Dictionary<string,Image>();
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

`;
s=s.replace('        private void FillChild',methods+'        private void FillChild');
fs.writeFileSync(f,s);
let st=p.join(root,'src/SelfTestV2.cs'),t=fs.readFileSync(st,'utf8');t=t.slice(0,t.indexOf('        // ---------------------------------------------------------------- 전송 계층'))+'    }\n}\n#endif\n';fs.writeFileSync(st,t);
