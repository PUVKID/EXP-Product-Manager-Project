using System; using BunjangAuto;
class R { static void Main(){
  var s = Storage.LoadSettings(); var l = Storage.LoadProducts(); var p = l[0];
  bool ok = s.OneByOneMinutes==27 && s.RepeatAll && s.RepeatCount==3 && s.UseTimeRules && s.AllowDuplicate && s.ChromePath==@"C:\x\chrome.exe" && s.AssumeLoggedIn && s.TimeRules.Count==1 && s.TimeRules[0].Minutes==15 && s.ThemeMode=="System";
  bool ok2 = l.Count==1 && p.Title=="구버전 상품 😀" && p.Price=="123000" && p.Category=="a>b>c" && p.Condition=="사용감 적음" && p.Quantity=="2" && p.Tags=="t1,t2" && p.Images=="a.jpg;b.jpg" && p.Description=="설명\r\n둘째줄" && p.FreeShipping && p.Status=="등록됨" && p.ProductId=="999" && p.LastPosted=="2026-09-01 10:00:00";
  Console.WriteLine("settings migrated: "+ok+"  products migrated: "+ok2);
  s.ThemeMode="Dark"; Storage.SaveSettings(s); Console.WriteLine("theme persisted: " + (Storage.LoadSettings().ThemeMode=="Dark"));
}}
