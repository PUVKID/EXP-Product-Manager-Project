using System;
using System.IO;
using System.Net.Mail;
using System.Threading;
using Lumivolt.Security;
namespace Lumivolt.Admin {
    public static class TrialIssuer {
        public static string Issue(string folder,string request,string email) {
            if(!AdminWorkflow.IsMatchingVault(folder))throw new InvalidOperationException("기존 발급키를 연결해 주세요.");
            email=(email??"").Trim().ToLowerInvariant();
            try { if(new MailAddress(email).Address!=email || email.Length>120)throw new FormatException(); } catch { throw new FormatException("체험판은 신청한 고객의 이메일을 입력해 주세요."); }
            request=LicenseText.Normalize(request,"LVR3"); var req=LicenseProtocol.VerifyRequest(request,DateTime.UtcNow);
            using(var mutex=new Mutex(false,"Local\\EXP_Trial_Issuer")) {
                bool entered=false;
                try {
                    try { entered=mutex.WaitOne(15000); }catch(AbandonedMutexException){entered=true;}
                    if(!entered)throw new IOException("다른 발급 작업이 진행 중입니다.");
                    string dir=Path.Combine(folder,"records");
                    if(Directory.Exists(dir))foreach(var file in Directory.GetFiles(dir,"*.lvlicense")) {
                        var old=IssuerService.Inspect(folder,LicenseProtocol.ReadFile(file));
                        if(old["trial"]!=null && (bool)old["trial"] && ((string)old["devicePublicKey"]==(string)req["devicePublicKey"] || String.Equals((string)old["name"],email,StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("이미 체험판을 발급한 이메일 또는 PC입니다. 기존 발급 이력을 확인해 주세요.");
                    }
                    return IssuerService.Issue(folder,request,email,DateTime.UtcNow.AddHours(3),"Pro",true);
                } finally { if(entered)mutex.ReleaseMutex(); }
            }
        }
    }
}
