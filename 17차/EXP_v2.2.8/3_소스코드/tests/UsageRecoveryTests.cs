using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using BunjangAuto;
class UsageRecoveryTests {
 static int passed,failed;
 static string file,reg;
 static DateTime now=DateTime.UtcNow;
 static MethodInfo read=typeof(UsageQuota).GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);
 static MethodInfo write=typeof(UsageQuota).GetMethod("Write",BindingFlags.NonPublic|BindingFlags.Static);
 static JObject Read(string device="device-a"){return (JObject)read.Invoke(null,new object[]{device,now});}
 static void Write(JObject state){write.Invoke(null,new object[]{state});}
 static byte[] RegistryData(){using(var k=Registry.CurrentUser.OpenSubKey(reg))return (byte[])k.GetValue("State");}
 static void SetRegistry(byte[] data){using(var k=Registry.CurrentUser.CreateSubKey(reg))k.SetValue("State",data,RegistryValueKind.Binary);}
 static void Check(string name,bool value){Console.WriteLine((value?"PASS ":"FAIL ")+name);if(value)passed++;else failed++;}
 static void Reject(string name,Action action){try{action();Check(name,false);}catch{Check(name,true);}}
 static bool Equal(byte[] a,byte[] b){return Convert.ToBase64String(a)==Convert.ToBase64String(b);}
 static int Main(){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};return Run();}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static int Run(){file=Path.Combine(Paths.Root,"usage.v1");reg=@"Software\EXP\Tests\"+System.Diagnostics.Process.GetCurrentProcess().Id;
  try {
   var s=Read();s["used"]=12;s["credits"]=7;s["trialId"]="trial-a";((JObject)s["redeemed"])["credit-a"]=true;Write(s);
   for(int i=0;i<3;i++){
    s=Read();s["used"]=(int)s["used"]+1;
    using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))Reject("injected file replacement failure "+i,()=>Write(s));
    var recovered=Read();
    Check("interrupted charge retained "+i,(int)recovered["used"]==13+i);
    Check("both copies repaired before next write "+i,Equal(File.ReadAllBytes(file),RegistryData()));
   }
   s=Read();Check("credits/trial/redemption retained",(long)s["credits"]==7&&(string)s["trialId"]=="trial-a"&&(bool)s["redeemed"]["credit-a"]);
   var old=File.ReadAllBytes(file);s["used"]=16;Write(s);var newer=File.ReadAllBytes(file);SetRegistry(old);
   Check("older registry recovers newest file",(int)Read()["used"]==16&&Equal(File.ReadAllBytes(file),RegistryData()));
   SetRegistry(old);Reject("wrong device rejected before repair",()=>Read("device-b"));Check("wrong device does not alter copies",Equal(RegistryData(),old)&&Equal(File.ReadAllBytes(file),newer));SetRegistry(newer);
   s=Read();Write(s);var newest=File.ReadAllBytes(file);SetRegistry(old);Reject("nonadjacent rollback still rejected",()=>Read());SetRegistry(newest);
   File.Delete(file);Reject("deleted file remains blocked",()=>Read());File.WriteAllBytes(file,newest);
   using(var k=Registry.CurrentUser.OpenSubKey(reg,true))k.DeleteValue("State");File.Delete(file);Reject("both copies deleted remains blocked",()=>Read());SetRegistry(newest);File.WriteAllBytes(file,newest);
   File.WriteAllBytes(file,new byte[100]);Reject("tampered DPAPI file remains blocked",()=>Read());File.WriteAllBytes(file,newest);
   Check("repaired ledger remains readable",(int)Read()["used"]==16);
  }catch(Exception ex){Console.WriteLine(ex);failed++;}
  finally{try{using(var k=CngKey.Open("EXP.Usage.Test."+System.Diagnostics.Process.GetCurrentProcess().Id,CngProvider.MicrosoftSoftwareKeyStorageProvider))k.Delete();}catch{}Registry.CurrentUser.DeleteSubKeyTree(reg,false);}
  Console.WriteLine("RECOVERY: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }
}
