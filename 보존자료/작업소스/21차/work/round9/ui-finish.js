const fs=require('fs'),p=require('path'),r=p.resolve('work/round9/Lumivolt_v2.2.0/3_소스코드');
function edit(f,fn){let q=p.join(r,f);fs.writeFileSync(q,fn(fs.readFileSync(q,'utf8').replace(/\r\n/g,'\n')).replace(/^\uFEFF/,''));}
edit('src/LicenseForm.cs',s=>s.replace('ClientSize = new Size(620, 620)','ClientSize = new Size(620, 768)').replace('                LastInfo = info;\n                return true;','                UsageQuota.AcceptTrial(info);\n                LastInfo = info;\n                return true;').replace('            AcceptButton = activate;',`            var trial = new SoftCard { Left=32,Top=578,Width=556,Height=142 };
            trial.Controls.Add(Design.Label("Pro 3시간 체험하기",24,16,500,26,12,"t"));
            trial.Controls.Add(Design.Label("요청 내용을 복사해 아래 이메일로 보내 주세요.\\r\\n받은 체험 키를 위에 입력하면 됩니다. PC·신청 이메일당 1회.",24,48,500,42,9.5f,"m"));
            var requestTrial=Design.Button("체험판 요청 복사",24,96,180,false); requestTrial.Height=32;
            var email=Design.Label(Brand.Email,214,102,320,24,10,"a");
            requestTrial.Click+=delegate { try { var code=TpmDevice.CreateRequest(); Clipboard.SetText("체험판 기능 사용하기\\r\\nPro 3시간 체험판을 신청합니다.\\r\\n등록코드: "+code); request.Text=code; notice("요청을 복사했습니다. "+Brand.Email+"로 보내 주세요.",true); } catch(Exception ex) { notice(TpmDevice.Error(ex),false); } };
            trial.Controls.AddRange(new Control[]{requestTrial,email}); Controls.Add(trial); file.Top=close.Top=736;
            AcceptButton = activate;`));
edit('src/Security/UsageQuota.cs',s=>s.replace('s["trialId"]=li.LicenseId; Write(s);','if(first==li.LicenseId)return true; s["trialId"]=li.LicenseId; Write(s);'));
edit('src/Security/LicenseFiles.cs',s=>s.replace('*.lvlicense','*.lvlicense;*.expkey'));
edit('src/UI/Pages/SchedulePage.cs',s=>s.replace('자정을 넘기려면 22:00 ~ 06:00 처럼 적고, 하루 끝은 24:00 입니다. 시간대 밖에서는 다음 시간대까지 기다립니다.','한국시간 기준 · 새 시간대 시작 시 대기 초기화 · 겹치면 나중에 시작한 시간대 우선').replace('bool one = chkOneByOne.Checked;',`bool one = chkOneByOne.Checked;
            var li=LicenseGate.LastInfo; bool advanced=li!=null && li.Valid && Lumivolt.Security.EditionPolicy.CanRepeat(li.Edition);
            chkDup.Enabled=advanced;`).replace('chkWindows.Enabled = one;','chkWindows.Enabled = one && advanced;').replace('gridRules.Enabled = one && chkWindows.Checked','gridRules.Enabled = one && advanced && chkWindows.Checked').replace('chkRepeat.Enabled = one;','chkRepeat.Enabled = one && advanced;').replace('numRounds.Enabled = one && chkRepeat.Checked','numRounds.Enabled = one && advanced && chkRepeat.Checked').replace('var sub = Ui.Muted("등록 방식과 간격, 반복을 정합니다. 바꾼 값은 자동으로 저장됩니다.");','var sub = Ui.Muted("반복·시간대 예약: Studio / Pro · 번개장터 서버 시각 동기화 후 실행됩니다.");'));
edit('src/UI/Pages/AutomationPage.cs',s=>s.replace('private readonly CardPanel cardMain, cardNums;','private readonly CardPanel cardMain, cardNums;\n        private Label usage, clock; private int quotaTick;').replace('Controls.AddRange(new Control[] { title, sub, cardMain, cardNums });',`usage=Ui.Heading(UsageQuota.Summary()); usage.Location=new Point(2,462);
            clock=Ui.Muted(ServerClock.Status); clock.Location=new Point(2,498);
            var policy=Ui.Muted("기본 한도: 자정 초기화 · 등록 버튼 제출 시 1회 차감 · 제출 후 결과 미확인도 차감 유지"); policy.Location=new Point(2,530);
            var contact=Ui.Muted("Pro 추가 등록권 문의  "+Brand.Email); contact.Location=new Point(2,560);
            Controls.AddRange(new Control[] { title, sub, cardMain, cardNums,usage,clock,policy,contact });`).replace('var a = Host.Auto; var r = Host.Runner;','if(clock!=null)clock.Text=ServerClock.Status; if(usage!=null && ++quotaTick%10==0)usage.Text=UsageQuota.Summary();\n            var a = Host.Auto; var r = Host.Runner;'));
edit('src/UI/Pages/LicensePage.cs',s=>s.replace('Controls.AddRange(new Control[] { title, sub, c1, c2 });',`var extra=new CardPanel {Left=0,Top=542,Height=178};
            var h3=Ui.Heading("Pro 추가 등록권"); h3.Location=new Point(20,16);
            var info=Ui.Muted("기본 한도를 다 쓰면 추가 등록권을 사용합니다. 미사용 잔액은 이월됩니다.");info.Location=new Point(22,48);
            var credit=Design.Input("creditKey",22,78,470,44,false);
            var redeem=Ui.Button("충전", "",BtnStyle.Primary,110);redeem.Location=new Point(510,80);
            var contact=Ui.Muted("추가 구매 문의  "+Brand.Email); contact.Location=new Point(22,138);
            redeem.Click+=delegate { try { UsageQuota.Redeem(credit.Text);credit.Clear();info.Text=UsageQuota.Summary(); }catch(Exception ex){info.Text=ex.Message;} };
            extra.Controls.AddRange(new Control[]{h3,info,credit,redeem,contact});
            Controls.AddRange(new Control[] { title, sub, c1, c2,extra });
            Resize+=delegate {extra.Width=Math.Max(Ui.S(400),ClientSize.Width-Padding.Horizontal);};`));
// User-facing help no longer exposes file formats; encrypted in-app storage stays compatible.
for(const f of fs.readdirSync(p.join(r,'resources/help'))) if(f.endsWith('.md')) edit('resources/help/'+f,s=>s.replace(/Lumivolt\(루미볼트\)|Lumivolt/g,'EXP').split('\n').filter(l=>!(/csv/i.test(l))).join('\n'));
fs.writeFileSync(p.join(r,'resources/help/05-csv.md'),`# 상품을 앱에서 관리하기

상품 관리에서 **상품 추가**를 누르고 제목·가격·설명·카테고리·사진을 작성하세요.
입력한 상품과 사진은 이 PC에 자동 저장됩니다. 목록의 상품을 선택하고 **편집**을 누르면 다시 수정할 수 있습니다.

## 이용 등급
- Classic: 하루 25개, 기본 상품 등록.
- Studio: 하루 50개, 반복 등록·시간대 예약.
- Pro: 하루 100개, 반복 등록·시간대 예약·추가 등록권.
- Pro 체험판: 발급 시점부터 3시간, PC와 신청 이메일당 1회.

기본 일일 잔여량은 이월되지 않으며 한국시간 자정에 초기화됩니다. 계정을 바꿔도 이 PC의 사용량은 합산됩니다. 계정별로는 하루 50개를 넘길 수 없습니다.
추가 등록권은 Pro에서 기본 한도를 사용한 뒤 차감하며 미사용 잔액은 유지됩니다. 구입 문의: lukeyun@kyonggi.ac.kr

등록 버튼을 제출하기 직전에 1회 기록합니다. 제출 전에 준비가 실패하면 차감하지 않습니다. 제출 뒤 결과를 확인하지 못한 경우에도 차감은 유지됩니다.
`);
fs.appendFileSync(p.join(r,'resources/help/20-faq.md'),`\n## 예약 시각과 체험판\n번개장터 HTTPS 응답의 시각을 동기화해 한국시간으로 예약합니다. 시각을 확인할 수 없으면 등록을 대기합니다. 내부 등록 한도 서버와의 시각 일치를 보증하는 기능은 아닙니다.\n08:50에 등록하고 45분 간격이더라도 다음 예약 시작이 09:00이면 이전 대기를 끝내고 09:00부터 다음 상품을 준비합니다. 브라우저 처리 시간 때문에 실제 게시 시각에는 차이가 있습니다.\n체험판은 첫 인증 화면의 요청 복사로 신청하세요. 발급 시각부터 3시간이며, 동일 PC/신청 이메일에는 재발급할 수 없습니다.\n`);
