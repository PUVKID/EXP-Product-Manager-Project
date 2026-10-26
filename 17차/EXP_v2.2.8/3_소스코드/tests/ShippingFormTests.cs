using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using BunjangAuto;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
class ShippingFormTests {
    static int passed;
    static readonly Dictionary<string,Assembly> loaded=new Dictionary<string,Assembly>();
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name){try{action();}catch(Exception){Check(true,name);return;}throw new Exception(name);}
    static void Apply(Bot bot,Product p){try{typeof(Bot).GetMethod("ConfigureShipping",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bot,new object[]{p});}catch(TargetInvocationException e){throw e.InnerException;}}
    static bool Selected(IWebDriver d,string css){return d.FindElement(By.CssSelector(css)).Selected;}
    static string Val(IWebDriver d,string id){return d.FindElement(By.Id(id)).GetAttribute("value");}
    static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string name=new AssemblyName(e.Name).Name;Assembly existing;if(loaded.TryGetValue(name,out existing))return existing;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);var assembly=Assembly.Load(m.ToArray());loaded[name]=assembly;return assembly;}}};try{Run(args);Console.WriteLine("SHIPPING: "+passed+" passed");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string[] args){
        var old=JsonConvert.DeserializeObject<Product>("{FreeShipping:true,Title:'기존 상품',Price:'1000'}");ShippingOptions.Validate(old);Check(old.FreeShipping&&!old.ShippingFee.HasValue,"legacy included shipping remains usable");
        Reject(()=>ShippingOptions.Validate(new Product()),"missing separate fee rejected");
        Reject(()=>ShippingOptions.Validate(new Product{ShippingFee=99}),"normal minimum enforced");
        Reject(()=>ShippingOptions.Validate(new Product{ShippingFee=30001}),"normal maximum enforced");
        Reject(()=>ShippingOptions.Validate(new Product{ShippingFee=3000,HalfShipping=true}),"half fee and carrier required");
        var p=new Product{Title="상품",Price="10000",ShippingFee=3500,HalfShipping=true,HalfShippingCarrier="GS_HALF_PRICE",HalfShippingFee=1800,RememberShipping=true};
        Paths.EnsureAll();Storage.SaveProducts(new List<Product>{p});var saved=Storage.LoadProducts().Single();
        Check(saved.ShippingFee==3500&&saved.HalfShipping&&saved.HalfShippingCarrier=="GS_HALF_PRICE"&&saved.HalfShippingFee==1800&&saved.RememberShipping,"encrypted product round trip preserves shipping");
        string fixture=Path.Combine(args[1],"shipping-fixture.html");Directory.CreateDirectory(args[1]);File.WriteAllText(fixture,Html);
        var service=ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(args[0]),Path.GetFileName(args[0]));service.HideCommandPromptWindow=true;
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1200,900");options.AddArgument("--no-first-run");options.AddArgument("--disable-background-networking");
        options.AddArgument("--user-data-dir="+Path.Combine(args[1],"fixture-profile"));
        using(var driver=new ChromeDriver(service,options))using(var bot=new Bot(new AppSettings(),new SelectorSet())){
            driver.Navigate().GoToUrl(new Uri(fixture).AbsoluteUri);typeof(Bot).GetField("_driver",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bot,driver);
            p.HalfShipping=false;Apply(bot,p);Check(Val(driver,"normal")=="3500"&&Selected(driver,"input[name=isPossible][value='0']"),"normal fee and half shipping disabled applied");
            foreach(var carrier in new[]{"ALL","GS_HALF_PRICE","CU_THRIFTY"}){
                p.HalfShipping=true;p.HalfShippingCarrier=carrier;Apply(bot,p);
                Check(driver.FindElement(By.Id("carrier")).Text==ShippingOptions.CarrierLabel(carrier)&&Val(driver,"halfFee")=="1800"&&Selected(driver,"input[name=isPossible][value='1']"),"carrier and half fee applied "+carrier);
            }
            p.RememberShipping=false;Apply(bot,p);Check(!Selected(driver,"#default-shipping-fee"),"remember can be cleared");
            p.FreeShipping=true;Apply(bot,p);Check(driver.FindElement(By.Id("method")).Text=="배송비 포함","explicitly switches back to included");
            p.FreeShipping=false;p.HalfShipping=false;p.ShippingFee=100;Apply(bot,p);Check(Val(driver,"normal")=="100","minimum normal fee applied");
            p.ShippingFee=30000;Apply(bot,p);Check(Val(driver,"normal")=="30000","maximum normal fee applied");
            p.HalfShipping=true;p.HalfShippingFee=4000;Reject(()=>Apply(bot,p),"live half fee range enforced before submit");
            p.HalfShippingFee=1800;
            ((IJavaScriptExecutor)driver).ExecuteScript("document.querySelector('input[value=\"1\"]').disabled=true;document.querySelector('input[value=\"0\"]').click();");
            Reject(()=>Apply(bot,p),"locked half shipping fails without fallback");
        }
    }
    const string Html=@"<!doctype html><meta charset='utf-8'><style>body{padding:30px}button,input,label{margin:8px}section{padding:20px}</style>
<button id='method' aria-haspopup='listbox' onclick='document.getElementById(""methods"").hidden=false'><span>배송비 포함</span></button>
<ul id='methods' hidden><li role='option' onclick='method(true)'>배송비 포함</li><li role='option' onclick='method(false)'>배송비 별도</li></ul>
<section id='scroll-trade-shippingSpecs' style='display:none'>일반택배 <input id='normal' type='text' inputmode='numeric' placeholder='100~30,000'>
<label><input type='radio' name='isPossible' value='0' checked hidden onchange='half(false)'>불가능</label><label><input type='radio' name='isPossible' value='1' hidden onchange='half(true)'>가능</label>
<div id='half' hidden><button id='carrier' aria-haspopup='listbox' onclick='document.getElementById(""carriers"").hidden=false'>둘 다 가능</button>
<ul id='carriers' hidden><li role='option' onclick='carrier(this)'>둘 다 가능</li><li role='option' onclick='carrier(this)'>GS25 만</li><li role='option' onclick='carrier(this)'>CU만</li></ul>
<input id='halfFee' type='text' inputmode='numeric' placeholder='100~3,000'></div></section>
<label><input id='default-shipping-fee' type='checkbox'>다음 등록시에도 사용</label>
<script>function method(v){document.getElementById('method').innerHTML='<span>배송비 '+(v?'포함':'별도')+'</span>';document.getElementById('methods').hidden=true;document.getElementById('scroll-trade-shippingSpecs').style.display=v?'none':'block';}function half(v){document.getElementById('half').hidden=!v;}function carrier(e){document.getElementById('carrier').textContent=e.textContent;document.getElementById('carriers').hidden=true;}</script>";
}
