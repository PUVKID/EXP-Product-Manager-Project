using System;
using System.Text;
namespace Lumivolt.Security
{
    // Transport convenience only. Payload bytes, signatures and verification remain unchanged.
    public static class LicenseText
    {
        public static string Normalize(string text, string prefix)
        {
            if (String.IsNullOrWhiteSpace(text)) throw new FormatException(prefix == "LVR3" ? "고객에게 받은 등록코드를 붙여 넣어 주세요." : "받은 인증키를 붙여 넣어 주세요.");
            if (text.Length > LicenseProtocol.MaxText) throw new FormatException("코드가 너무 깁니다. 코드만 복사해 주세요.");
            var b = new StringBuilder();
            foreach (char c in text) if (!Char.IsWhiteSpace(c) && c != '\u200B' && c != '\uFEFF') b.Append(c);
            string token = b.ToString();
            if (!(token.StartsWith(prefix + ".", StringComparison.Ordinal) || (prefix == "LV3" && token.StartsWith("LV4.", StringComparison.Ordinal)))) throw new FormatException(prefix == "LVR3" ? "등록코드가 아닙니다. 고객 앱의 ‘등록코드 복사’를 사용해 주세요." : "인증키가 아닙니다. 판매자에게 받은 인증키를 입력해 주세요.");
            return token;
        }
    }
}
