const fs=require('fs'),p=require('path');
const root=p.resolve('work/round11/source'),old=p.resolve('work/round9/Lumivolt_v2.2.0/3_소스코드');
fs.mkdirSync(root,{recursive:true});
fs.cpSync(old,root,{recursive:true,filter:f=>!['dist','bin','obj'].includes(p.basename(f))});
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const f=p.join(dir,e.name);if(e.isDirectory())walk(f);else if(/\.(cs|md|txt|iss|csproj)$/.test(f)){const s=fs.readFileSync(f,'utf8'),n=s.replaceAll('lukeyun@kyonggi.ac.kr','expmasterxx00@gmail.com').replaceAll('2.2.0','2.2.1');if(n!==s)fs.writeFileSync(f,n);}}}walk(root);
function edit(f,fn){const path=p.join(root,f);fs.writeFileSync(path,fn(fs.readFileSync(path,'utf8')));}
edit('src/LicenseForm.cs',s=>s.replace(/            Controls.Add\(new BrandMark[\s\S]*?            var first =/,`            Controls.Add(new HeroBanner { Left=32, Top=24, Width=556, Height=136, Eyebrow="EXP  /  WELCOME", Title="시작은 가볍게.", Subtitle="등록코드를 보내고, 받은 키를 붙여 넣으세요." });
            var first =`));
edit('admin/LicenseAdmin.cs',s=>s.replace('new Size(980,760)','new Size(980,808)').replace(/        Controls.Add\(new BrandMark[\s\S]*?        var light=/,`        Controls.Add(new HeroBanner { Left=32,Top=24,Width=916,Height=128,Eyebrow="EXP MASTER  /  윤석",Title="키 발급, 더 간결하게.",Subtitle="등록코드 하나로 제품과 기간을 연결하세요." });
        var light=`).replace('754,36','754,164').replace('852,36','852,164').replace('34,116','34,170').replaceAll('Top=160','Top=216').replace('34,694','34,748').replace('850,696','850,748'));
edit('src/UI/Pages/DashboardPage.cs',s=>s.replace('private readonly StatCard cPending','private readonly HeroBanner hero;\n        private readonly StatCard cPending').replace(/            var title = Ui.Title[\s\S]*?            grid =/,`            hero = new HeroBanner { Left=0,Top=0,Height=148,Width=940,Eyebrow="EXP  /  YOUR WORKSPACE",Title="오늘의 판매, 가볍게.",Subtitle="상품을 준비하고, 나머지는 EXP에 맡기세요.",ActionText="상품 준비하기  →",TabStop=true,Cursor=Cursors.Hand,AccessibleRole=AccessibleRole.PushButton,AccessibleName="상품 준비하기" };
            hero.Click += delegate { Host.Navigate("products"); };
            grid =`).replace('new Point(0, 74)','new Point(0, 166)').replace(/            var quick = Ui.Button[\s\S]*?            Resize \+= delegate \{ Relayout\(\); \};/,`            Controls.AddRange(new Control[] { hero, grid, activity, recentLogs });
            Resize += delegate { Relayout(); };`).replace('grid.Width = w + Ui.S(12);','hero.Width = w;\n            grid.Width = w + Ui.S(12);').replace('Padding.Top + Ui.S(300)','Padding.Top + Ui.S(392)'));
edit('src/UI/Controls.cs',s=>s.replace('Theme.RoundedRect(r, S(5))','Theme.RoundedRect(r, S(12))').replace('new SolidBrush(_sel ? p.Selected : p.Hover)','new SolidBrush(_sel ? p.Accent : p.Hover)').replace('Color fore = _sel ? p.Text : p.TextMuted;','Color fore = _sel ? p.OnAccent : p.TextMuted;').replace('_sel ? p.Accent : fore','fore').replace('_sel ? p.Text : p.Text, LeftFlags','_sel ? p.OnAccent : p.Text, LeftFlags'));
edit('src/Theme.cs',s=>s.replace('246,247,249','245,246,250').replace('237,239,243','236,239,246').replace('29,87,204','31,88,225').replace('19,21,26','16,19,28').replace('15,17,21','19,23,34').replace('29,32,39','28,33,47'));
console.log(root);
