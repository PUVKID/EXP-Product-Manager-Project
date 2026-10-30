using System;using System.IO;using System.Reflection;using System.Security.Cryptography;using Microsoft.Win32;using Newtonsoft.Json.Linq;using BunjangAuto;using Lumivolt.Security;
class RepairWindowsUsage {
 static string root=AppDomain.CurrentDomain.BaseDirectory;
 static void Main(){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object s,ResolveEventArgs e){var app=Assembly.LoadFile(Path.Combine(root,"EXP.exe"));using(var stream=app.GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(stream==null)return null;using(var m=new MemoryStream()){stream.CopyTo(m);return Assembly.Load(m.ToArray());}}};using(var w=new StreamWriter(Path.Combine(root,"실제Windows기록복구.txt"))){try{Run(w);}catch(Exception ex){w.WriteLine("FAILED "+ex);Environment.ExitCode=1;}}}
 static JObject Decode(byte[] bytes){return (JObject)typeof(UsageQuota).GetMethod("Decode",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{bytes});}
 static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void Run(TextWriter w){using(var mutex=new System.Threading.Mutex(false,"Local\\EXP_Usage_v1")){
 bool held=false;try{held=mutex.WaitOne(15000);Require(held,"Usage lock unavailable");
 var file=Path.Combine(Paths.Root,"usage.v1");var data=File.ReadAllBytes(file);var state=Decode(data);
 var li=License.Verify(File.ReadAllText(Path.Combine(Paths.Root,"license.key")),"");Require(li.Valid,"License/TPM verification failed");
 Require(li.MachineId=="0751362B19EF716E94B21AE136AEF184FCEDC7F598F98FCF4C70F0525BC4245D","Unexpected enrollment; repair stopped");
 Require((string)state["device"]==li.MachineId,"File does not belong to current enrollment");
 Require((long)state["seq"]==2&&(int)state["used"]==0&&(long)state["credits"]==0&&((JObject)state["accounts"]).Count==0&&((JObject)state["redeemed"]).Count==0&&state["trialId"]==null,"Current ledger changed; repair stopped");
 using(var k=Registry.CurrentUser.OpenSubKey(@"Software\EXP\Usage",true)){
 Require(k!=null,"Missing native registry ledger");var old=(byte[])k.GetValue("State");var legacy=Decode(old);
 Require((string)legacy["device"]=="DD2E0CB9C04A653B76BC8607CE445A0ACCBCB4FBB7ADFDE8BAC28D9CBC7E6D2E"&&(long)legacy["seq"]==2&&(string)legacy["lastUtc"]=="2026-10-12T17:53:37Z","Unexpected native ledger; repair stopped");
 Require((int)legacy["used"]==0&&(long)legacy["credits"]==0&&((JObject)legacy["accounts"]).Count==0&&((JObject)legacy["redeemed"]).Count==0&&legacy["trialId"]==null,"Historical usage requires manual reconciliation");
 var backup=Path.Combine(root,"초기화전_실제레지스트리.dpapi");Require(!File.Exists(backup),"Backup already exists; do not repeat repair");File.WriteAllBytes(backup,old);
 k.SetValue("State",data,RegistryValueKind.Binary);k.Flush();Require(Convert.ToBase64String((byte[])k.GetValue("State"))==Convert.ToBase64String(data),"Registry write verification failed");
 }
 Require(Convert.ToBase64String(File.ReadAllBytes(file))==Convert.ToBase64String(data),"Usage file changed");
 w.WriteLine("PASS: native registry stale enrollment copy replaced with verified current file.");w.WriteLine("PASS: license, TPM enrollment, usage file, counts, credits and trial state preserved.");
 }finally{if(held)mutex.ReleaseMutex();}}}
}