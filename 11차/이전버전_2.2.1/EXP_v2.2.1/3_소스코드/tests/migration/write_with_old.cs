using System; using System.ComponentModel; using BunjangAuto;
class W { static void Main(){
  Paths.EnsureAll();
  var s = new AppSettings{ OneByOneMinutes=27, RepeatAll=true, RepeatCount=3, UseTimeRules=true, AllowDuplicate=true, ChromePath=@"C:\x\chrome.exe", AssumeLoggedIn=true};
  s.TimeRules.Add(new TimeRule("02:00","06:00",15));
  Storage.SaveSettings(s);
  var l = new BindingList<Product>();
  l.Add(new Product{Title="구버전 상품 😀", Price="123000", Category="a>b>c", Condition="사용감 적음", Quantity="2", Tags="t1,t2", Images="a.jpg;b.jpg", Description="설명\r\n둘째줄", FreeShipping=true, Status="등록됨", ProductId="999", LastPosted="2026-09-01 10:00:00"});
  Storage.SaveProducts(l);
  Console.WriteLine("WROTE with OLD version");
}}
