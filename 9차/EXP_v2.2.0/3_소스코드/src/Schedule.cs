using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BunjangAuto
{
    /// <summary>
    /// 시간대 한 줄. "시작 ~ 종료 사이에는 Minutes 분마다 1개씩 등록".
    /// 종료가 시작보다 이르면 자정을 넘기는 시간대(예: 22:00~06:00), 같으면 24시간 내내로 본다.
    /// </summary>
    public class TimeRule
    {
        public string Start { get; set; }
        public string End { get; set; }
        public int Minutes { get; set; }

        public TimeRule() { Start = "00:00"; End = "09:00"; Minutes = 40; }
        public TimeRule(string start, string end, int minutes) { Start = start; End = end; Minutes = minutes; }
    }

    /// <summary>검사가 끝난 시간대(분 단위).</summary>
    public class TimeWindow
    {
        public int Start;     // 0 ~ 1439
        public int End;       // 0 ~ 1440
        public int Minutes;   // 등록 간격(분)

        public bool Contains(double minuteOfDay)
        {
            if (Start == End) return true;                       // 24시간
            if (Start < End) return minuteOfDay >= Start && minuteOfDay < End;
            return minuteOfDay >= Start || minuteOfDay < End;    // 자정을 넘김
        }

        public override string ToString()
        {
            return Schedule.Format(Start) + "~" + Schedule.Format(End) + " " + Minutes + "분마다 1개";
        }
    }

    public static class Schedule
    {
        /// <summary>"9", "9:30", "09:30", "24:00" 형태를 분으로 바꾼다.</summary>
        public static bool TryParseTime(string s, out int minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace('：', ':').Replace('.', ':');
            string[] parts = s.Split(':');
            int h, m = 0;
            if (parts.Length == 1)
            {
                if (!int.TryParse(parts[0], out h)) return false;
            }
            else if (parts.Length == 2)
            {
                if (!int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return false;
            }
            else return false;
            if (h < 0 || h > 24 || m < 0 || m > 59) return false;
            if (h == 24 && m != 0) return false;
            minutes = h * 60 + m;
            return true;
        }

        public static string Format(int minutes)
        {
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        /// <summary>화면에서 입력한 시간대 목록을 검사해 계산용 목록으로 바꾼다. 문제가 있으면 false 와 안내 문구.</summary>
        public static bool TryBuild(IList<TimeRule> rules, out List<TimeWindow> wins, out string error)
        {
            wins = new List<TimeWindow>();
            error = null;
            if (rules == null || rules.Count == 0)
            {
                error = "시간대가 하나도 없습니다. [시간대 추가]를 눌러 시간대를 넣어 주세요.";
                return false;
            }
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                string no = (i + 1) + "번째 시간대: ";
                int s, e;
                if (!TryParseTime(r.Start, out s) || s >= 1440)
                { error = no + "시작 시각을 'HH:mm' 형태로 넣어 주세요. (예: 00:00)"; return false; }
                if (!TryParseTime(r.End, out e))
                { error = no + "종료 시각을 'HH:mm' 형태로 넣어 주세요. (예: 09:00, 하루 끝은 24:00)"; return false; }
                if (r.Minutes < 1 || r.Minutes > 1440)
                { error = no + "간격은 1~1440분 사이로 넣어 주세요."; return false; }
                wins.Add(new TimeWindow { Start = s, End = e, Minutes = r.Minutes });
            }
            return true;
        }

        public static TimeWindow ActiveAt(IList<TimeWindow> wins, DateTime t)
        {
            double m = t.TimeOfDay.TotalMinutes;
            // For overlapping rules, the most recently started window takes priority.
            TimeWindow best = null; DateTime latest = DateTime.MinValue;
            foreach (var w in wins) if (w.Contains(m)) {
                DateTime start = t.Date.AddMinutes(w.Start); if (start > t) start = start.AddDays(-1);
                if (start > latest) { latest = start; best = w; }
            }
            return best;
        }

        /// <summary>t 가 어떤 시간대 안이면 t, 아니면 그 이후 가장 가까운 시간대 시작 시각.</summary>
        public static DateTime NextAllowed(IList<TimeWindow> wins, DateTime t)
        {
            if (ActiveAt(wins, t) != null) return t;
            DateTime best = DateTime.MaxValue;
            foreach (var w in wins)
            {
                for (int d = 0; d <= 1; d++)
                {
                    DateTime s = t.Date.AddDays(d).AddMinutes(w.Start);
                    if (s >= t && s < best) best = s;
                }
            }
            return best;
        }

        /// <summary>
        /// 직전 등록을 시작한 시각(lastStart)을 기준으로 다음 등록 시각을 구한다.
        /// 직전 시각이 속한 시간대의 간격만큼 뒤로 잡고, 그 시각이 시간대 밖이면 다음 시간대가 시작될 때까지 기다린다.
        /// </summary>
        public static DateTime NextRun(IList<TimeWindow> wins, DateTime lastStart, DateTime now, int fallbackMinutes)
        {
            var w = ActiveAt(wins, lastStart);
            int gap = w != null ? w.Minutes : fallbackMinutes;
            DateTime candidate = lastStart.AddMinutes(gap);
            // A new reservation window cancels the previous window's remaining delay,
            // including adjacent windows, gaps, overlaps and midnight boundaries.
            foreach (var rule in wins) for (int d = 0; d <= 2; d++) {
                DateTime start = lastStart.Date.AddDays(d).AddMinutes(rule.Start);
                if (start > lastStart && start < candidate) candidate = start;
            }
            DateTime cand = NextAllowed(wins, candidate);
            if (cand < now) cand = NextAllowed(wins, now);
            return cand;
        }

        public static string Describe(IList<TimeWindow> wins)
        {
            return string.Join(" / ", wins.Select(w => w.ToString()).ToArray());
        }
    }
}
