using System;
namespace Lumivolt.Security {
    public static class EditionPolicy {
        public static int DailyLimit(string edition) {
            switch (edition) { case "Classic": return 25; case "Studio": return 50; case "Pro": return 100; default: throw new FormatException("지원하지 않는 EXP 등급입니다."); }
        }
        public static bool CanRepeat(string edition) { DailyLimit(edition); return edition != "Classic"; }
        public static int SetupCode(string edition) { DailyLimit(edition); return edition == "Classic" ? 10 : edition == "Studio" ? 20 : 30; }
    }
}
