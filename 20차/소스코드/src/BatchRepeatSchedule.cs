using System;

namespace BunjangAuto
{
    /// <summary>전체 목록 반복의 입력과 다음 미래 슬롯을 계산한다. 시계와 실행 상태를 변경하지 않는다.</summary>
    public static class BatchRepeatSchedule
    {
        public static bool TryValidate(AppSettings settings, out string error)
        {
            error = null;
            if (!settings.BatchRepeatEnabled) return true;
            if (settings.BatchRepeatClockAligned)
            {
                if (settings.BatchRepeatMinuteOfHour < 0 || settings.BatchRepeatMinuteOfHour > 59)
                    error = "매시 지정분은 0~59 사이로 넣어 주세요.";
            }
            else if (settings.BatchRepeatMinutes < 1 || settings.BatchRepeatMinutes > 1440)
                error = "전체 목록 반복 간격은 1~1440분 사이로 넣어 주세요.";
            return error == null;
        }

        public static DateTime NextAfter(DateTime anchor, DateTime finished, bool clockAligned, int minutes, int minuteOfHour)
        {
            if (clockAligned)
            {
                if (minuteOfHour < 0 || minuteOfHour > 59) throw new ArgumentOutOfRangeException("minuteOfHour");
                DateTime next = finished.Date.AddHours(finished.Hour).AddMinutes(minuteOfHour);
                return next > finished ? next : next.AddHours(1);
            }
            if (minutes < 1 || minutes > 1440) throw new ArgumentOutOfRangeException("minutes");
            long gap = TimeSpan.TicksPerMinute * minutes;
            long elapsed = Math.Max(0, finished.Ticks - anchor.Ticks);
            long slots = elapsed / gap + 1;
            return anchor.AddTicks(checked(slots * gap));
        }
    }
}
