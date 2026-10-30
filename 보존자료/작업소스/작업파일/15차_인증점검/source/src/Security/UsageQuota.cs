using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Lumivolt.Security;
namespace BunjangAuto {
    // Offline, per enrolled Windows user/device. This is not a server or TPM monotonic counter.
    // Two DPAPI copies detect single-copy rollback/deletion. A persistent Platform KSP marker
    // prevents resetting usage simply by deleting both copies. An administrator can still
    // roll back an entire machine snapshot; no offline design claims to prevent that.
    public static class UsageQuota {
        static readonly byte[] Entropy=LicenseProtocol.Utf8.GetBytes("EXP.Usage.v1");
        static string FilePath { get { return Path.Combine(Paths.Root,"usage.v1"); } }
        static string RegistryPath { get {
#if TESTBUILD
            return @"Software\EXP\Tests\"+System.Diagnostics.Process.GetCurrentProcess().Id;
#else
            return @"Software\EXP\Usage";
#endif
        } }
        static string MarkerName { get {
#if TESTBUILD
            return "EXP.Usage.Test."+System.Diagnostics.Process.GetCurrentProcess().Id;
#else
            return "EXP.Usage.v1";
#endif
        } }
        static CngProvider Provider { get {
#if TESTBUILD
            return CngProvider.MicrosoftSoftwareKeyStorageProvider;
#else
            return new CngProvider("Microsoft Platform Crypto Provider");
#endif
        } }
        static string Day(DateTime utc) { return utc.AddHours(9).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture); }
        static T Locked<T>(Func<T> action) {
            using(var m=new Mutex(false,"Local\\EXP_Usage_v1")) {
                bool entered=false;
                try { try { entered=m.WaitOne(15000); } catch(AbandonedMutexException) { entered=true; } if(!entered) throw new IOException("사용량 기록을 처리 중입니다. 잠시 후 다시 시도해 주세요."); return action(); }
                finally { if(entered) m.ReleaseMutex(); }
            }
        }
        static JObject Decode(byte[] bytes) {
            if(bytes==null || bytes.Length>4000000) throw new IOException("사용량 기록 크기 오류");
            var plain=ProtectedData.Unprotect(bytes,Entropy,DataProtectionScope.CurrentUser);
            // Usage history can exceed license payload limits; use strict JSON duplicate handling.
            using(var sr=new StringReader(LicenseProtocol.Utf8.GetString(plain))) using(var r=new Newtonsoft.Json.JsonTextReader(sr) { DateParseHandling=Newtonsoft.Json.DateParseHandling.None,MaxDepth=8 }) {
                var o=JObject.Load(r,new Newtonsoft.Json.Linq.JsonLoadSettings { DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error });
                if(r.Read() || (int)o["version"]!=1 || (long)o["seq"]<0 || (int)o["used"]<0 || (long)o["credits"]<0 || !(o["redeemed"] is JObject) || !(o["accounts"] is JObject)) throw new IOException("사용량 기록 형식 오류"); return o;
            }
        }
        static void AuditRead(string phase,string device,JObject state,byte[] file,byte[] reg,bool marker) {
            try {
                string target=Path.Combine(Paths.Root,"activation-diagnostic.txt");
                if(File.Exists(target) && new FileInfo(target).Length>64000) File.Delete(target);
                string stored=state==null?"":(string)state["device"];
                string line=DateTime.UtcNow.ToString("o")+" | "+phase+" | pid="+System.Diagnostics.Process.GetCurrentProcess().Id+" | root="+Paths.Root+" | expected="+device+" | stored="+stored+" | file="+(file==null?0:file.Length)+" | registry="+(reg==null?0:reg.Length)+" | marker="+marker+" | seq="+(state==null?"":Convert.ToString(state["seq"]))+Environment.NewLine;
                Directory.CreateDirectory(Paths.Root);File.AppendAllText(target,line);
            } catch { }
        }
        static JObject Read(string device,DateTime now) {
            byte[] file=File.Exists(FilePath)?File.ReadAllBytes(FilePath):null, reg=null;
            using(var k=Registry.CurrentUser.OpenSubKey(RegistryPath)) if(k!=null) reg=k.GetValue("State") as byte[];
            bool marker=CngKey.Exists(MarkerName,Provider); AuditRead("read",device,null,file,reg,marker);
            if(file==null && reg==null) {
                if(marker) throw new IOException("사용량 기록이 없어 등록을 중단했습니다. 관리자에게 복구를 문의해 주세요.");
                return new JObject {{"version",1},{"seq",0},{"device",device},{"day",Day(now)},{"lastUtc",LicenseProtocol.Utc(now)},{"used",0},{"credits",0},{"accounts",new JObject()},{"redeemed",new JObject()}};
            }
            // An interrupted initial write can be recovered from one authenticated copy. Once
            // the marker exists, a missing copy is never treated as a fresh allowance.
            if(marker && (file==null || reg==null)) throw new IOException("사용량 기록 일부가 삭제되었습니다. 관리자에게 문의해 주세요.");
            JObject a=file==null?null:Decode(file), b=reg==null?null:Decode(reg), o=a??b;
            byte[] currentData=file??reg;
            bool repair=file==null || reg==null;
            if(a!=null && b!=null) {
                if((long)a["seq"]==(long)b["seq"]) { if(!JToken.DeepEquals(a,b)) throw new IOException("사용량 기록이 일치하지 않습니다."); }
                else {
                    var newer=(long)a["seq"]>(long)b["seq"]?a:b; var older=ReferenceEquals(newer,a)?b:a;
                    if((long)newer["seq"]!=(long)older["seq"]+1 || (string)newer["previous"]!=LicenseProtocol.Fingerprint(LicenseProtocol.Utf8.GetBytes(older.ToString(Newtonsoft.Json.Formatting.None)))) throw new IOException("사용량 기록 되돌리기가 감지되었습니다.");
                    o=newer; // Recover only an adjacent, hash-linked interrupted commit, using the newer charge.
                    currentData=ReferenceEquals(newer,a)?file:reg;
                    repair=true;
                }
            }
            o.AddAnnotation(LicenseProtocol.Fingerprint(LicenseProtocol.Utf8.GetBytes(o.ToString(Newtonsoft.Json.Formatting.None))));
            AuditRead("compare",device,o,file,reg,marker); if((string)o["device"]!=device) throw new IOException("다른 PC의 사용량 기록입니다.");
            if(now.AddMinutes(5)<LicenseProtocol.Time((string)o["lastUtc"])) throw new IOException("PC 시간이 이전으로 변경되었습니다.");
            int order=String.CompareOrdinal(Day(now),(string)o["day"]);
            if(order<0) throw new IOException("날짜를 확인해 주세요.");
            // Complete the interrupted commit BEFORE another sequence is issued. Otherwise,
            // two file replacement failures leave a gap of two and look like a rollback.
            // Only already authenticated, device/time-checked adjacent states are repaired.
            if(repair) PersistCopies(currentData);
            if(order>0) { o["day"]=Day(now); o["used"]=0; o["accounts"]=new JObject(); }
            return o;
        }
        static void Write(JObject state) {
            string previous=state.Annotation<string>() ?? ""; state["seq"]=(long)state["seq"]+1;
            state["previous"]=previous;
            byte[] data=ProtectedData.Protect(LicenseProtocol.Utf8.GetBytes(state.ToString(Newtonsoft.Json.Formatting.None)),Entropy,DataProtectionScope.CurrentUser);
            PersistCopies(data);
            if(!CngKey.Exists(MarkerName,Provider)) using(var key=CngKey.Create(CngAlgorithm.ECDsaP256,MarkerName,new CngKeyCreationParameters { Provider=Provider,ExportPolicy=CngExportPolicies.None,KeyUsage=CngKeyUsages.Signing })) { }
        }
        static void PersistCopies(byte[] data) {
            Directory.CreateDirectory(Paths.Root); string temp=FilePath+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)) { f.Write(data,0,data.Length); f.Flush(true); }
            // Registry first; if the process stops between writes, Read selects this newer state.
            using(var k=Registry.CurrentUser.CreateSubKey(RegistryPath)) { k.SetValue("State",data,RegistryValueKind.Binary); k.Flush(); }
            if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
        }
        static LicenseInfo RequireLicense() { if(!LicenseGate.Recheck()) throw new InvalidOperationException("유효한 라이선스가 필요합니다."); return LicenseGate.LastInfo; }
        public static void RequireMode(AppSettings cfg,bool includeRegistered) {
            var li=RequireLicense(); if(!EditionPolicy.CanRepeat(li.Edition) && (cfg.RepeatAll || cfg.AllowDuplicate || includeRegistered || cfg.UseTimeRules)) throw new InvalidOperationException("반복·중복 등록과 시간대 예약은 Studio / Pro에서 사용할 수 있습니다.");
        }
        public static void EnsureReady() { var li=RequireLicense(); Locked(()=>{var s=Read(li.MachineId,ServerClock.UtcNow); Write(s); return true;}); }
        public static void AcceptTrial(LicenseInfo li) {
            if(!li.Valid || !li.Trial)return;
            Locked(()=> { var s=Read(li.MachineId,ServerClock.UtcNow); string first=(string)s["trialId"];
                if(first!=null && first!=li.LicenseId)throw new InvalidOperationException("이 PC는 이미 체험판을 사용했습니다. 정식 라이선스를 등록해 주세요.");
                if(first==li.LicenseId)return true; s["trialId"]=li.LicenseId; Write(s); return true;
            });
        }
        public static void Consume(string shopId) {
            ServerClock.RequireFresh();
            var li=RequireLicense(); Locked(()=>{ var s=Read(li.MachineId,ServerClock.UtcNow); ApplyConsume(s,li.Edition,shopId); s["lastUtc"]=LicenseProtocol.Utc(ServerClock.UtcNow); Write(s); return true; });
        }
        internal static void ApplyConsume(JObject state,string edition,string shopId) {
            if(String.IsNullOrWhiteSpace(shopId) || !System.Text.RegularExpressions.Regex.IsMatch(shopId,@"^\d{1,20}$")) throw new InvalidOperationException("현재 로그인한 내 상점을 확인할 수 없어 등록을 중단했습니다.");
            var accounts=(JObject)state["accounts"]; string account=LicenseProtocol.Fingerprint(LicenseProtocol.Utf8.GetBytes(shopId)); int used=accounts[account]==null?0:(int)accounts[account];
            if(used>=50) throw new InvalidOperationException("이 계정의 오늘 등록 한도 50개를 모두 사용했습니다.");
            int total=(int)state["used"], cap=EditionPolicy.DailyLimit(edition);
            if(total>=cap) { if(edition!="Pro" || (long)state["credits"]<=0) throw new InvalidOperationException("오늘 EXP 등록 한도를 모두 사용했습니다. 추가 등록 문의: "+Brand.Email); state["credits"]=(long)state["credits"]-1; }
            state["used"]=checked(total+1); accounts[account]=used+1;
        }
        public static string Summary() {
            var li=LicenseGate.LastInfo; if(li==null || !li.Valid) return "라이선스 인증 후 사용량이 표시됩니다.";
            try { return Locked(()=> { var s=Read(li.MachineId,ServerClock.UtcNow); return "오늘 "+(int)s["used"]+" / "+EditionPolicy.DailyLimit(li.Edition)+"개  ·  추가 등록권 "+(long)s["credits"]+"개"; }); }
            catch(Exception ex) { return "사용량 확인 필요: "+ex.Message; }
        }
        public static void Redeem(string token) {
            var li=RequireLicense(); if(li.Edition!="Pro") throw new InvalidOperationException("추가 등록권은 Pro에서 사용할 수 있습니다.");
            var credit=CreditProtocol.Verify(token,TrustedKeys.All,ServerClock.UtcNow); TpmDevice.Prove((string)credit["devicePublicKey"]);
            Locked(()=> { var s=Read(li.MachineId,ServerClock.UtcNow); ApplyCredit(s,credit); s["lastUtc"]=LicenseProtocol.Utc(ServerClock.UtcNow); Write(s); return true; });
        }
        internal static void ApplyCredit(JObject state,JObject credit) {
            var redeemed=(JObject)state["redeemed"]; string id=(string)credit["creditId"];
            if(redeemed[id]!=null) throw new InvalidOperationException("이미 충전한 추가 등록권입니다.");
            state["credits"]=checked((long)state["credits"]+(long)credit["quantity"]); redeemed[id]=true;
        }
    }
}
