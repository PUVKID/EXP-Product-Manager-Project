using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>
    /// 실행 / 예약 로직. 예전 MainForm 의 StartClicked / LaunchRun / OnRunnerFinished / OnTimer ... 를
    /// 화면(컨트롤)과 분리해 그대로 옮긴 것이다. 동작(1개씩 등록, 시간대별 등록, 반복, 중복 허용, 자동 종료, 다음 실행 시각)은 동일하다.
    /// 화면은 Changed 이벤트를 받아 상태 문구/진행률/버튼 활성 상태를 다시 읽는다.
    /// </summary>
    public class AutomationController
    {
        public readonly AppSettings Cfg;
        public readonly SelectorSet Sel;
        public readonly BindingList<Product> Products;
        public readonly Runner Runner;
        private readonly Control _ui;

        public BindingList<TimeRule> Rules = new BindingList<TimeRule>();
        private List<TimeWindow> _wins;
        private int _round = 1, _roundTotal = 1;
        private DateTime _runStart = DateTime.MinValue;
        private readonly HashSet<Product> _skip = new HashSet<Product>();
        private DateTime _nextRun = DateTime.MinValue;
        private bool _scheduleOn;

        // ---- 화면이 읽는 상태
        public string StateText = "대기 중";
        public string NextText = "";
        public int Done, Total;
        public bool Active;                         // 시작 ~ 종료 사이(예약 대기 포함)
        public bool Paused { get { return Runner.Paused; } }
        public DateTime NextRun { get { return _nextRun; } }
        public int Round { get { return _round; } }
        public int RoundTotal { get { return _roundTotal; } }
        public int SuccessThisRun, FailThisRun;

        public event Action Changed;
        public event Action ProductsChanged;
        /// <summary>안내 창(메시지, 제목)</summary>
        public Action<string, string> Notice;

        public AutomationController(AppSettings cfg, SelectorSet sel, BindingList<Product> products, Runner runner, Control ui)
        {
            Cfg = cfg; Sel = sel; Products = products; Runner = runner; _ui = ui;
            LoadRulesFromSettings();
            Runner.Finished += OnRunnerFinished;
            Runner.Progress += OnRunnerProgress;
        }

        private void Raise() { var h = Changed; if (h != null) h(); }

        // ============================ 설정 ↔ 목록 ============================
        public void LoadRulesFromSettings()
        {
            Rules.Clear();
            if (Cfg.TimeRules != null) foreach (var r in Cfg.TimeRules) Rules.Add(new TimeRule(r.Start, r.End, r.Minutes));
            if (Rules.Count == 0)
            {
                // 처음 쓰는 경우의 예시 시간대
                Rules.Add(new TimeRule("00:00", "09:00", 40));
                Rules.Add(new TimeRule("09:00", "18:00", 60));
            }
        }

        /// <summary>화면에서 바꾼 값(Cfg 의 필드들 + 시간대 표)을 암호화 저장한다. (예전 ReadUiToSettings 의 저장 부분)</summary>
        public void SaveSettings()
        {
            Cfg.TimeRules = Rules.Select(r => new TimeRule(r.Start, r.End, r.Minutes)).ToList();
            Storage.SaveSettings(Cfg);
        }

        // ============================ 실행 / 예약 ============================
        public void Start()
        {
            SaveSettings();
            if (!Cfg.DoRegister) { Say("실행할 작업을 선택해 주세요.", "안내"); return; }
            _skip.Clear();
            _round = 1;
            _roundTotal = (Cfg.OneByOne && Cfg.RepeatAll) ? Math.Max(1, Cfg.RepeatCount) : 1;
            _wins = null;
            SuccessThisRun = 0; FailThisRun = 0;
            if (Cfg.OneByOne && Cfg.UseTimeRules)
            {
                string err; List<TimeWindow> wins;
                if (!Schedule.TryBuild(Cfg.TimeRules, out wins, out err)) { Say(err, "시간대 설정 확인"); return; }
                _wins = wins;
                Log.Write("[시간대별 등록] " + Schedule.Describe(_wins));
            }
            if (_roundTotal > 1) Log.Write("[반복] 목록을 총 " + _roundTotal + "회 올립니다. (1/" + _roundTotal + "회차 시작)");
            _scheduleOn = Cfg.OneByOne;
            Active = true;
            Log.Write("INFO", "Automation", "run.start", "자동등록을 시작합니다. (" + (Cfg.OneByOne ? (Cfg.UseTimeRules ? "1개씩·시간대별" : "1개씩·" + Cfg.OneByOneMinutes + "분 간격") : "일괄") + (_roundTotal > 1 ? ", 총 " + _roundTotal + "회 반복" : "") + (Cfg.AllowDuplicate ? ", 중복 허용" : "") + ")", "");

            if (_wins != null && Schedule.ActiveAt(_wins, DateTime.Now) == null)
            {
                // 지금은 등록 시간대가 아니므로 다음 시간대가 시작될 때까지 기다린다
                _nextRun = Schedule.NextAllowed(_wins, DateTime.Now);
                StateText = "시간대 시작 대기 중";
                Log.Write("[시간대별 등록] 지금은 등록 시간대가 아니어서 " + _nextRun.ToString("MM-dd HH:mm") + " 에 시작합니다.");
                Raise();
                return;
            }
            LaunchRun();
        }

        private void LaunchRun()
        {
            StateText = "실행 중...";
            NextText = "";
            Done = 0; Total = 0;
            _runStart = DateTime.Now;
            bool includeRegistered = Cfg.AllowDuplicate || _round > 1;   // 2회차부터는 이미 등록된 상품도 처음부터 다시
            Raise();
            Runner.Run(Cfg, Sel, Products, () => Products.Where(p => !_skip.Contains(p)).ToList(), () => Storage.SaveProducts(Products), includeRegistered);
        }

        public void Stop()
        {
            _scheduleOn = false;
            _nextRun = DateTime.MinValue;
            Runner.Resume();            // 일시정지 중이어도 중지가 바로 먹도록 먼저 해제
            Runner.Stop();
            Log.Write("INFO", "Automation", "run.stop.request", "[중지] 중지를 요청했습니다.", "");
            StateText = "중지하는 중...";
            if (!Runner.Running) FinishUi();
            Raise();
        }

        public void Pause() { Runner.Pause(); if (Runner.Running) StateText = "일시정지됨"; Raise(); }
        public void Resume() { Runner.Resume(); if (Runner.Running) StateText = "실행 중..."; Raise(); }

        /// <summary>이번 회차에 아직 등록할 상품이 남았는지</summary>
        private bool HasPending()
        {
            bool includeRegistered = Cfg.AllowDuplicate || _round > 1;
            return Products.Any(p => p.IsValid && !_skip.Contains(p) &&
                (includeRegistered || (string.IsNullOrEmpty(p.ProductId) && p.Status != "등록됨")));
        }

        public int RemainingCount
        {
            get
            {
                bool includeRegistered = Cfg.AllowDuplicate || _round > 1;
                return Products.Count(p => p.IsValid && !_skip.Contains(p) &&
                    (includeRegistered || (string.IsNullOrEmpty(p.ProductId) && p.Status != "등록됨")));
            }
        }

        public string RoundText() { return _roundTotal > 1 ? "  ·  " + _round + "/" + _roundTotal + "회차" : ""; }

        private void OnRunnerFinished()
        {
            if (_ui.IsDisposed) return;
            Post(delegate
            {
                // 이번 실행에서 성공/실패 집계
                foreach (var p in Runner.LastRegistered)
                {
                    if (p.Status == "등록됨") SuccessThisRun++;
                    else if ((p.Status ?? "").StartsWith("실패")) FailThisRun++;
                }
                if (ProductsChanged != null) ProductsChanged();
                if (!_scheduleOn) { FinishUi(); Raise(); return; }

                // 이번 회차에 이미 시도한 상품(성공/실패)은 같은 회차 안에서 다시 시도하지 않는다
                foreach (var p in Runner.LastRegistered) _skip.Add(p);
                int failed = _skip.Count(p => (p.Status ?? "").StartsWith("실패"));

                bool pending = HasPending();
                if (!pending)
                {
                    if (_round < _roundTotal)
                    {
                        _round++;
                        _skip.Clear();
                        Log.Write("[반복] " + _round + "/" + _roundTotal + "회차 시작 — 처음 상품부터 다시 등록합니다.");
                        pending = HasPending();
                    }
                    if (!pending)
                    {
                        _scheduleOn = false;
                        _nextRun = DateTime.MinValue;
                        Log.Write("[1개씩 등록] 모두 등록해 종료합니다." + (_roundTotal > 1 ? " (총 " + _roundTotal + "회 반복 완료)" : "") +
                                  (failed > 0 ? " (이번 회차에 실패한 상품 " + failed + "건)" : ""));
                        FinishUi();
                        Raise();
                        return;
                    }
                }

                if (_wins != null) _nextRun = Schedule.NextRun(_wins, _runStart, DateTime.Now, Cfg.OneByOneMinutes);
                else
                {
                    _nextRun = _runStart.AddMinutes(Cfg.OneByOneMinutes);
                    if (_nextRun < DateTime.Now) _nextRun = DateTime.Now;
                }
                Log.Write("[1개씩 등록] 다음 등록 예정: " + _nextRun.ToString(_nextRun.Date == DateTime.Today ? "HH:mm:ss" : "MM-dd HH:mm:ss"));
                StateText = "다음 실행 대기 중" + RoundText();
                Raise();
            });
        }

        private void FinishUi()
        {
            if (Active) Log.Write("INFO", "Automation", "run.finish", "자동등록이 끝났습니다. (성공 " + SuccessThisRun + " / 실패 " + FailThisRun + ")", "");
            Active = false;
            StateText = "대기 중";
            NextText = "";
            Done = 0; Total = 0;
        }

        private void OnRunnerProgress(int done, int total)
        {
            if (_ui.IsDisposed) return;
            Post(delegate
            {
                Done = done; Total = total;
                StateText = (Runner.Paused ? "일시정지됨" : "실행 중...") + " (" + done + "/" + total + ")" + RoundText();
                if (ProductsChanged != null) ProductsChanged();
                Raise();
            });
        }

        /// <summary>1초마다 UI 스레드에서 호출: 다음 실행 시각이 되면 다음 상품을 올린다.</summary>
        public void Tick()
        {
            if (!_scheduleOn || Runner.Running || _nextRun == DateTime.MinValue) return;
            var left = _nextRun - DateTime.Now;
            if (left.TotalSeconds <= 0)
            {
                _nextRun = DateTime.MinValue;
                LaunchRun();
            }
            else
            {
                string t = left.TotalHours >= 1
                    ? ((int)left.TotalHours) + "시간 " + left.Minutes + "분 " + left.Seconds + "초"
                    : left.Minutes + "분 " + left.Seconds + "초";
                NextText = "다음 실행까지 " + t + "  (" + _nextRun.ToString("MM-dd HH:mm") + ")";
                Raise();
            }
        }

        private void Say(string msg, string title) { var n = Notice; if (n != null) n(msg, title); }

        private void Post(Action a)
        {
            try { if (_ui.IsHandleCreated) _ui.BeginInvoke(a); } catch { }
        }
    }
}
