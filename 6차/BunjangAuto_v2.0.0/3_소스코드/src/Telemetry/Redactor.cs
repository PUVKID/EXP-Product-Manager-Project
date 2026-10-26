using System;
using System.Text.RegularExpressions;

namespace BunjangAuto
{
    /// <summary>
    /// 중앙 서버로 보내기 직전에 로그 문자열에서 비밀정보로 보이는 값을 지운다.
    /// (비밀번호, 쿠키, 세션/OAuth 토큰, Authorization 헤더, 개인키, 라이선스 키, 카드·계좌번호, 사용자 폴더명)
    /// 로컬 로그 파일은 이 처리를 거치지 않는다(원문 그대로 보존).
    /// </summary>
    public static class Redactor
    {
        public const string Mask = "[REDACTED]";

        private static readonly Regex[] Patterns = new Regex[]
        {
            // PEM / XML 개인키
            new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)", RegexOptions.Compiled),
            new Regex(@"<RSAKeyValue>[\s\S]*?(</RSAKeyValue>|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"<(P|Q|DP|DQ|InverseQ|D)>[A-Za-z0-9+/=\s]{20,}</\1>", RegexOptions.Compiled),
            // Authorization / Cookie 헤더 전체
            new Regex(@"(?i)\b(authorization|proxy-authorization|cookie|set-cookie)\s*[:=]\s*[^\r\n]+", RegexOptions.Compiled),
            // Bearer / Basic 토큰
            new Regex(@"(?i)\b(bearer|basic)\s+[A-Za-z0-9\-._~+/]{8,}=*", RegexOptions.Compiled),
            // key=value 형태의 비밀 항목 (password, token, secret, session, sid, otp 등)
            new Regex(@"(?i)(pass(?:word|wd)?|pwd|비밀번호|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|id[_-]?token|token|session(?:id)?|sessid|sid|jsessionid|sso|otp|인증번호|oauth[_\w]*|credential[s]?)(""?\s*[:=]\s*""?)([^\s&;,""'}\]]+)", RegexOptions.Compiled),
            // JWT
            new Regex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b", RegexOptions.Compiled),
            // 라이선스 키(Base64.Base64 로 길게 이어진 값)
            new Regex(@"\b[A-Za-z0-9+/]{40,}={0,2}\.[A-Za-z0-9+/]{100,}={0,2}", RegexOptions.Compiled),
            // 긴 Base64/Hex 덩어리(토큰·키로 의심)
            new Regex(@"\b[A-Fa-f0-9]{40,}\b", RegexOptions.Compiled),
            new Regex(@"\b[A-Za-z0-9+/_-]{60,}={0,2}", RegexOptions.Compiled),
            // 이메일 주소
            new Regex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled),
        };

        private static readonly Regex CardLike = new Regex(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)", RegexOptions.Compiled);
        // 은행 계좌번호처럼 하이픈으로 나뉜 긴 숫자(예: 110-123-456789)
        private static readonly Regex AccountLike = new Regex(@"(?<!\d)\d{2,6}-\d{2,6}-\d{3,8}(?:-\d{1,4})?(?!\d)", RegexOptions.Compiled);
        // C:\Users\홍길동\... → C:\Users\<user>\...
        private static readonly Regex UserDir = new Regex(@"(?i)([A-Z]:\\Users\\)[^\\/:*?""<>|\r\n]+", RegexOptions.Compiled);
        private static readonly Regex UserDir2 = new Regex(@"(?i)(/(?:home|Users)/)[^/\s]+", RegexOptions.Compiled);

        public static string Scrub(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            // 1) 사용자 폴더명 (다른 패턴이 경로를 잘라먹기 전에)
            s = UserDir.Replace(s, "$1<user>");
            s = UserDir2.Replace(s, "$1<user>");

            // 2) 패턴들
            for (int i = 0; i < Patterns.Length; i++)
            {
                var p = Patterns[i];
                if (i == 5) s = p.Replace(s, m => m.Groups[1].Value + m.Groups[2].Value + Mask);
                else if (i == 3) s = p.Replace(s, m => m.Groups[1].Value + ": " + Mask);
                else if (i == 4) s = p.Replace(s, m => m.Groups[1].Value + " " + Mask);
                else s = p.Replace(s, Mask);
            }

            // 3) 카드번호(Luhn 검사 통과) / 계좌번호 모양
            s = CardLike.Replace(s, m => LuhnOk(m.Value) ? Mask : m.Value);
            s = AccountLike.Replace(s, Mask);
            return s;
        }

        private static bool LuhnOk(string raw)
        {
            int sum = 0, n = 0; bool alt = false;
            for (int i = raw.Length - 1; i >= 0; i--)
            {
                char c = raw[i];
                if (c < '0' || c > '9') continue;
                int d = c - '0';
                if (alt) { d *= 2; if (d > 9) d -= 9; }
                sum += d; alt = !alt; n++;
            }
            return n >= 13 && n <= 19 && sum % 10 == 0;
        }

        public static void ScrubEntry(LogEntry e)
        {
            e.Message = Scrub(e.Message);
            e.Module = Scrub(e.Module);
            e.Action = Scrub(e.Action);
            e.ExcType = Scrub(e.ExcType);
        }
    }
}
