using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
namespace Lumivolt.Security {
    public static class CreditProtocol {
        public static JObject Verify(string text, IDictionary<string,byte[]> trusted, DateTime now) {
            byte[] data, sig; LicenseProtocol.Decode(LicenseText.Normalize(text,"EXPC1"),"EXPC1",out data,out sig);
            var o = LicenseProtocol.Parse(data); byte[] pub;
            if (!trusted.TryGetValue(LicenseProtocol.Text(o,"keyId",32),out pub)) throw new CryptographicException("등록되지 않은 발급키입니다.");
            var v = new Ed25519Signer(); v.Init(false,new Ed25519PublicKeyParameters(pub,0)); v.BlockUpdate(data,0,data.Length);
            if (!v.VerifySignature(sig)) throw new CryptographicException("추가 등록권 서명이 올바르지 않습니다.");
            var fields = "version,product,creditId,devicePublicKey,keyId,issuedUtc,quantity".Split(',');
            if (o.Count != fields.Length) throw new FormatException("추가 등록권 형식 오류");
            foreach (var f in fields) if (o[f] == null) throw new FormatException("추가 등록권 필드 오류");
            if (o["version"].Type != JTokenType.Integer || (int)o["version"] != 1 || LicenseProtocol.Text(o,"product",16) != "EXP") throw new FormatException("추가 등록권 제품 오류");
            Guid.ParseExact(LicenseProtocol.Text(o,"creditId",36),"D"); LicenseProtocol.PublicBlob(LicenseProtocol.Text(o,"devicePublicKey",128));
            if (LicenseProtocol.Time(LicenseProtocol.Text(o,"issuedUtc",32)) > now.AddMinutes(5)) throw new FormatException("발급 시각 이전입니다.");
            if (o["quantity"].Type != JTokenType.Integer || (long)o["quantity"] < 1 || (long)o["quantity"] > 100000) throw new FormatException("추가 등록권 수량 오류");
            return o;
        }
    }
}
