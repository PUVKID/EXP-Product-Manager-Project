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
            int start, end;
            if (error == null && settings.BatchRepeatWindowEnabled && !TryWindow(settings, out start, out end))
                error = "실행 시간은 HH:mm으로 입력해 주세요. 시작은 00:00~23:59, 종료는 00:00~24:00이며 시작과 종료는 달라야 합니다.";
            return error == null;
        }

        static bool TryMinute(string text, bool allowEndOfDay, out int minute)
        {
            minute = 0;
            if (allowEndOfDay && text == "24:00") { minute = 1440; return true; }
            TimeSpan value;
            if (text == null || text.Length != 5 || !TimeSpan.TryParseExact(text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out value)) return false;
            minute = (int)value.TotalMinutes;
            return minute >= 0 && minute < 1440;
        }
        static bool TryWindow(AppSettings cfg, out int start, out int end)
        {
            start = end = 0;
            return TryMinute(cfg.BatchRepeatWindowStart, false, out start) && TryMinute(cfg.BatchRepeatWindowEnd, true, out end) && start != end && !(start == 0 && end == 0);
        }
        public static bool WindowApplies(AppSettings cfg) { return cfg.BatchRepeatEnabled && cfg.BatchRepeatWindowEnabled; }
        public static bool IsAllowed(AppSettings cfg, DateTime now)
        {
            if (!WindowApplies(cfg)) return true;
            int start, end;
            if (!TryWindow(cfg, out start, out end)) return false;
            var time = now.TimeOfDay;
            return start < end ? time >= TimeSpan.FromMinutes(start) && time < TimeSpan.FromMinutes(end)
                : time >= TimeSpan.FromMinutes(start) || time < TimeSpan.FromMinutes(end);
        }
        static DateTime NextOpening(AppSettings cfg, DateTime now)
        {
            int start, end;
            if (!TryWindow(cfg, out start, out end)) throw new ArgumentException("실행 시간 범위가 올바르지 않습니다.");
            var opening = now.Date.AddMinutes(start);
            return opening >= now ? opening : opening.AddDays(1);
        }
        /// <summary>시작 기준 간격을 보존하면서 허용 시간에 포함되는 첫 슬롯을 찾는다.</summary>
        public static DateTime AtOrAfter(AppSettings cfg, DateTime anchor, DateTime now)
        {
            string error;
            if (!TryValidate(cfg, out error)) throw new ArgumentException(error);
            DateTime candidate;
            if (cfg.BatchRepeatClockAligned)
            {
                candidate = now.Date.AddHours(now.Hour).AddMinutes(cfg.BatchRepeatMinuteOfHour);
                if (candidate < now) candidate = candidate.AddHours(1);
            }
            else
            {
                long gap = TimeSpan.TicksPerMinute * cfg.BatchRepeatMinutes;
                long elapsed = Math.Max(0, now.Ticks - anchor.Ticks);
                long slots = elapsed / gap + (elapsed % gap == 0 ? 0 : 1);
                candidate = anchor.AddTicks(checked(slots * gap));
            }
            // 일일 간격의 위상은 최대 1440일이면 반복된다. 슬롯이 없는 조합도 무한 대기하지 않는다.
            for (int day = 0; day <= 1440; day++)
            {
                if (IsAllowed(cfg, candidate)) return candidate;
                var opening = NextOpening(cfg, candidate);
                if (cfg.BatchRepeatClockAligned)
                {
                    candidate = opening.Date.AddHours(opening.Hour).AddMinutes(cfg.BatchRepeatMinuteOfHour);
                    if (candidate < opening) candidate = candidate.AddHours(1);
                }
                else
                {
                    long gap = TimeSpan.TicksPerMinute * cfg.BatchRepeatMinutes;
                    long elapsed = Math.Max(0, opening.Ticks - anchor.Ticks);
                    candidate = anchor.AddTicks(checked((elapsed / gap + (elapsed % gap == 0 ? 0 : 1)) * gap));
                }
            }
            throw new ArgumentException("선택한 간격의 실행 시각이 허용 시간에 없습니다. 간격 또는 실행 시간을 바꿔 주세요.");
        }
        public static DateTime NextBatch(AppSettings cfg, DateTime anchor, DateTime finished)
        {
            DateTime next = NextAfter(anchor, finished, cfg.BatchRepeatClockAligned, cfg.BatchRepeatMinutes, cfg.BatchRepeatMinuteOfHour);
            return WindowApplies(cfg) ? AtOrAfter(cfg, anchor, next) : next;
        }
        public static string WindowSummary(AppSettings cfg)
        {
            return "매일 " + cfg.BatchRepeatWindowStart + " ~ " + cfg.BatchRepeatWindowEnd + (String.CompareOrdinal(cfg.BatchRepeatWindowStart, cfg.BatchRepeatWindowEnd) > 0 ? " (다음 날 종료)" : "") + (cfg.BatchRepeatWindowEnd == "24:00" ? " (자정 종료)" : "");
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
