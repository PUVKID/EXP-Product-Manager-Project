using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using BunjangAuto;
using Lumivolt.Security;
using Lumivolt.Admin;
class ReleaseTests {
    static int pass,fail;
    static void Check(string name,bool good){Console.WriteLine((good?"PASS ":"FAIL ")+name);if(good)pass++;else fail++;}
    static void Reject(string name,Action a){try{a();Check(name,false);}catch{Check(name,true);}}
    static string Sign(string folder,JObject o,string prefix){var key=IssuerService.Load(folder);var b=LicenseProtocol.Bytes(o);var signer=new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();signer.Init(true,key);signer.BlockUpdate(b,0,b.Length);return LicenseProtocol.Wrap(prefix,b,signer.GenerateSignature());}
    static JObject State(){return new JObject{{"used",0},{"credits",0},{"accounts",new JObject()},{"redeemed",new JObject()}};}
    [STAThread] static int Main(string[] args){ AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};return Run(); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(){
        string old=IssuerService.LastVault, dir=Path.Combine(Path.GetTempPath(),"EXP-release-test-"+Guid.NewGuid().ToString("N"));
        try {
            Paths.EnsureAll(); IssuerService.Create(dir); var pub=IssuerService.Public(dir); TrustedKeys.All.Add((string)pub["keyId"],Convert.FromBase64String((string)pub["publicKey"]));
            string req=TpmDevice.CreateRequest();
            foreach(string edition in new[]{"Classic","Studio","Pro"}) {
                string token=AdminWorkflow.Issue(dir,req,"",DateTime.UtcNow.AddDays(1),edition);
                var info=License.Verify(token,"");Check("signed edition "+edition,info.Valid&&info.Edition==edition);
                Check("whitespace paste "+edition,License.Verify("\n "+token.Insert(50," \r\n")+"\n","").Valid);
                var data=IssuerService.Inspect(dir,token); data["edition"]="Enterprise";Reject("unknown signed edition",()=>LicenseProtocol.VerifyLicense(Sign(dir,data,"LV4"),TrustedKeys.All,DateTime.UtcNow,"automation"));
                byte[] bytes,sig;LicenseProtocol.Decode(token,"LV4",out bytes,out sig);data["edition"]="Pro";data["name"]="tampered";
                Reject("edition tampering",()=>LicenseProtocol.VerifyLicense(LicenseProtocol.Wrap("LV4",LicenseProtocol.Bytes(data),sig),TrustedKeys.All,DateTime.UtcNow,"automation"));
            }
            string trial=TrialIssuer.Issue(dir,req,"trial@example.com");var trialData=IssuerService.Inspect(dir,trial);
            var issued=LicenseProtocol.Time((string)trialData["issuedUtc"]);var expiry=LicenseProtocol.Time((string)trialData["expiresUtc"]);
            Check("trial exactly three hours",expiry-issued==TimeSpan.FromHours(3));Check("trial is Pro",(bool)trialData["trial"]&&(string)trialData["edition"]=="Pro");
            Reject("trial expired at exact boundary",()=>LicenseProtocol.VerifyLicense(trial,TrustedKeys.All,expiry,"automation"));
            Reject("same device different email trial",()=>TrialIssuer.Issue(dir,req,"other@example.com"));
            Reject("same email trial",()=>TrialIssuer.Issue(dir,req,"TRIAL@example.com"));
            trialData["expiresUtc"]=LicenseProtocol.Utc(expiry.AddSeconds(1));Reject("signed trial duration over three hours",()=>LicenseProtocol.VerifyLicense(Sign(dir,trialData,"LV4"),TrustedKeys.All,issued,"automation"));
            string message;Check("trial activation",LicenseGate.TryActivate(trial,out message));Check("same trial can reopen without restarting",LicenseGate.TryActivate(trial,out message));
            trialData["expiresUtc"]=LicenseProtocol.Utc(expiry);trialData["licenseId"]=Guid.NewGuid().ToString("D");
            Check("second validly signed trial blocked on client",!LicenseGate.TryActivate(Sign(dir,trialData,"LV4"),out message));
            Check("regular Pro upgrade",LicenseGate.TryActivate(AdminWorkflow.Issue(dir,req,"",DateTime.UtcNow.AddDays(1),"Pro"),out message));
            var state=State();for(int i=0;i<25;i++)UsageQuota.ApplyConsume(state,"Classic",i%2==0?"1":"2");Reject("Classic 25 across switched accounts",()=>UsageQuota.ApplyConsume(state,"Classic","3"));
            state=State();for(int i=0;i<50;i++)UsageQuota.ApplyConsume(state,"Studio",i%2==0?"1":"2");Reject("Studio 50 across accounts",()=>UsageQuota.ApplyConsume(state,"Studio","3"));
            state=State();for(int i=0;i<100;i++)UsageQuota.ApplyConsume(state,"Pro",i<50?"1":"2");Reject("Pro 100 across accounts",()=>UsageQuota.ApplyConsume(state,"Pro","3"));
            string top=CreditIssuer.Issue(dir,req,3);var credit=CreditProtocol.Verify(top,TrustedKeys.All,DateTime.UtcNow);UsageQuota.ApplyCredit(state,credit);
            Reject("top-up replay",()=>UsageQuota.ApplyCredit(state,credit));Reject("credits do not bypass account 50",()=>UsageQuota.ApplyConsume(state,"Pro","1"));
            UsageQuota.ApplyConsume(state,"Pro","3");Check("one extra credit consumed",(long)state["credits"]==2&&(int)state["used"]==101);
            state["used"]=0;state["accounts"]=new JObject();UsageQuota.ApplyConsume(state,"Pro","3");Check("base allowance before credits next day",(int)state["used"]==1&&(long)state["credits"]==2);
            credit["quantity"]=9999;byte[] payload,signature;LicenseProtocol.Decode(top,"EXPC1",out payload,out signature);Reject("credit quantity tamper",()=>CreditProtocol.Verify(LicenseProtocol.Wrap("EXPC1",LicenseProtocol.Bytes(credit),signature),TrustedKeys.All,DateTime.UtcNow));
            UsageQuota.Redeem(top);Reject("persisted top-up replay",()=>UsageQuota.Redeem(top));
            Check("repeat classic denied",!EditionPolicy.CanRepeat("Classic"));Check("repeat Studio and Pro",EditionPolicy.CanRepeat("Studio")&&EditionPolicy.CanRepeat("Pro"));
            var classic=AdminWorkflow.Issue(dir,req,"",DateTime.UtcNow.AddDays(1),"Classic");LicenseGate.TryActivate(classic,out message);Reject("Classic runtime repeat guard",()=>UsageQuota.RequireMode(new AppSettings{RepeatAll=true},false));
            var usedPath=Path.Combine(Paths.Root,"usage.v1");var saved=File.ReadAllBytes(usedPath);File.WriteAllBytes(usedPath,new byte[100]);Reject("DPAPI usage tamper",()=>UsageQuota.EnsureReady());File.WriteAllBytes(usedPath,saved);
            File.Delete(usedPath);Reject("usage file deletion does not reset allowance",()=>UsageQuota.EnsureReady());File.WriteAllBytes(usedPath,saved);
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\EXP\Tests\"+System.Diagnostics.Process.GetCurrentProcess().Id,true)){var reg=(byte[])key.GetValue("State");key.DeleteValue("State");File.Delete(usedPath);Reject("both usage copies deletion blocked by enrollment marker",()=>UsageQuota.EnsureReady());key.SetValue("State",reg,RegistryValueKind.Binary);File.WriteAllBytes(usedPath,saved);}
            Check("restored usage remains valid",UsageQuota.Summary().Contains("3개"));
            var read=typeof(UsageQuota).GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);
            var write=typeof(UsageQuota).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic);
            var persisted=(JObject)read.Invoke(null,new object[]{LicenseGate.LastInfo.MachineId,DateTime.UtcNow});
            persisted["used"]=88; ((JObject)persisted["accounts"])["test-account"]=44; write.Invoke(null,new object[]{persisted});
            var tomorrow=(JObject)read.Invoke(null,new object[]{LicenseGate.LastInfo.MachineId,DateTime.UtcNow.AddDays(1)});
            Check("persisted next-day reset without base rollover",(int)tomorrow["used"]==0&&((JObject)tomorrow["accounts"]).Count==0&&(long)tomorrow["credits"]==3);
            var later=(JObject)read.Invoke(null,new object[]{LicenseGate.LastInfo.MachineId,DateTime.UtcNow.AddDays(30)});
            Check("30 inactive days do not accumulate base allowance",(int)later["used"]==0&&(long)later["credits"]==3);
            List<TimeWindow> wins;string error;var d=new DateTime(2026,10,13);
            Schedule.TryBuild(new[]{new TimeRule("00:00","09:00",45),new TimeRule("09:00","18:00",45)},out wins,out error);
            Check("08:50 plus 45 resets at 09:00",Schedule.NextRun(wins,d.AddHours(8).AddMinutes(50),d.AddHours(8).AddMinutes(51),45)==d.AddHours(9));
            Check("late completion runs immediately within active window",Schedule.NextRun(wins,d.AddHours(8).AddMinutes(50),d.AddHours(9).AddMinutes(5),45)==d.AddHours(9).AddMinutes(5));
            Schedule.TryBuild(new[]{new TimeRule("22:00","09:00",45),new TimeRule("09:00","18:00",60)},out wins,out error);
            Check("overnight reservation boundary",Schedule.NextRun(wins,d.AddHours(8).AddMinutes(50),d.AddHours(8).AddMinutes(55),45)==d.AddHours(9));
            Schedule.TryBuild(new[]{new TimeRule("12:00","09:00",45),new TimeRule("09:00","18:00",60)},out wins,out error);
            Check("overlapping rules latest start wins",Schedule.ActiveAt(wins,d.AddHours(13)).Minutes==45);
            Check("installer edition mapping",EditionPolicy.SetupCode("Classic")==10&&EditionPolicy.SetupCode("Studio")==20&&EditionPolicy.SetupCode("Pro")==30);
            ServerClock.RefreshAsync(); for(int i=0;i<120&&!ServerClock.Fresh;i++)Thread.Sleep(100);
            Check("live HTTPS server clock",ServerClock.Fresh); if(ServerClock.Fresh){var before=ServerClock.UtcNow;Thread.Sleep(50);Check("server clock advances monotonically",ServerClock.UtcNow>before);}
        } catch(Exception ex){Console.WriteLine(ex);fail++;}
        finally { IssuerService.LastVault=old;try{Directory.Delete(dir,true);}catch{}try{using(var k=CngKey.Open("EXP.Usage.Test."+System.Diagnostics.Process.GetCurrentProcess().Id,CngProvider.MicrosoftSoftwareKeyStorageProvider))k.Delete();Registry.CurrentUser.DeleteSubKeyTree(@"Software\EXP\Tests\"+System.Diagnostics.Process.GetCurrentProcess().Id,false);}catch{} }
        Console.WriteLine("RELEASE: "+pass+" passed, "+fail+" failed");return fail==0?0:1;
    }
}
