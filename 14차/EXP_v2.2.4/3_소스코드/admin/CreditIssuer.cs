using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Signers;
using Lumivolt.Security;
namespace Lumivolt.Admin {
    public static class CreditIssuer {
        public static string Issue(string folder,string request,int quantity) {
            if (!AdminWorkflow.IsMatchingVault(folder)) throw new InvalidOperationException("이 앱과 일치하는 발급키가 필요합니다.");
            if (quantity < 1 || quantity > 100000) throw new FormatException("수량은 1~100000 사이여야 합니다.");
            var req=LicenseProtocol.VerifyRequest(LicenseText.Normalize(request,"LVR3"),DateTime.UtcNow);
            var key=IssuerService.Load(folder); var pub=key.GeneratePublicKey().GetEncoded();
            var o=new JObject { {"version",1},{"product","EXP"},{"creditId",Guid.NewGuid().ToString("D")},{"devicePublicKey",req["devicePublicKey"].DeepClone()},{"keyId",LicenseProtocol.KeyId(pub)},{"issuedUtc",LicenseProtocol.Utc(DateTime.UtcNow)},{"quantity",quantity} };
            var data=LicenseProtocol.Bytes(o); var s=new Ed25519Signer(); s.Init(true,key); s.BlockUpdate(data,0,data.Length);
            var token=LicenseProtocol.Wrap("EXPC1",data,s.GenerateSignature());
            CreditProtocol.Verify(token,new Dictionary<string,byte[]> {{LicenseProtocol.KeyId(pub),pub}},DateTime.UtcNow);
            string dir=Path.Combine(folder,"credits"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,(string)o["creditId"]+".expcredit"),token,LicenseProtocol.Utf8); return token;
        }
    }
}
