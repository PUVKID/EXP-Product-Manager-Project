using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using BunjangAuto;

// 실제 Controller / Runner / UsageQuota를 함께 컴파일한다. 외부 브라우저·인증·시각만 격리한다.
class BatchRepeatTests
{
    static int passed, failed;
    static string selection = "";
    static Fixture currentFixture;
    static void Check(string name, bool value)
    {
        Console.WriteLine((value ? "PASS " : "FAIL ") + name);
        if (value) passed++;
        else { failed++; if (currentFixture != null) Console.WriteLine("실패 상태 " + currentFixture.Snapshot(name)); }
    }
    static void Set(AppSettings cfg, string name, object value) { var f = typeof(AppSettings).GetField(name); if (f != null) f.SetValue(cfg, value); }
    static AppSettings Batch(bool clock = false, int gap = 60, int minute = 0)
    {
        var cfg = new AppSettings { OneByOne = false };
        Set(cfg, "BatchRepeatEnabled", true); Set(cfg, "BatchRepeatClockAligned", clock);
        Set(cfg, "BatchRepeatMinutes", gap); Set(cfg, "BatchRepeatMinuteOfHour", minute); return cfg;
    }
    static BindingList<Product> Items(int count, bool registered = false)
    {
        return new BindingList<Product>(Enumerable.Range(0, count).Select(i => new Product { Title = "상품" + i, Price = "1000", ProductId = registered ? "기존" + i : "", Status = registered ? "등록됨" : "" }).ToList());
    }
    static bool Until(Func<bool> condition, int milliseconds = 2000)
    {
        var timer = Stopwatch.StartNew();
        do { Application.DoEvents(); if (condition()) return true; Thread.Sleep(2); } while (timer.ElapsedMilliseconds < milliseconds);
        return false;
    }
    static void Seed(int used = 0)
    {
        LicenseGate.Allowed = true; LicenseGate.LastInfo.Edition = "Pro";
        var read = typeof(UsageQuota).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static);
        var write = typeof(UsageQuota).GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Static);
        var state = (JObject)read.Invoke(null, new object[] { "isolated-device", ServerClock.UtcNow });
        state["used"] = used; state["credits"] = 0; write.Invoke(null, new object[] { state });
    }
    static void ResetLedger()
    {
        string process = Process.GetCurrentProcess().Id.ToString();
        string project = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
        string work = Path.Combine(project, "work");
        string expected = Path.GetFullPath(Path.Combine(work, "batch-ledger-" + process));
        if (!File.Exists(Path.Combine(project, ".codex", "routing-project.json")) || !expected.StartsWith(work + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("검사 프로젝트 내부 경로가 아닙니다.");
        if (!string.Equals(Path.GetFullPath(Paths.Root), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("검사 전용 임시 경로가 아닙니다.");
        string registry = @"Software\EXP\Tests\" + process;
        string marker = "EXP.Usage.Test." + process;
        if (CngKey.Exists(marker, CngProvider.MicrosoftSoftwareKeyStorageProvider))
            using (var key = CngKey.Open(marker, CngProvider.MicrosoftSoftwareKeyStorageProvider)) key.Delete();
        Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        foreach (string name in new[] { "usage.v1", "usage.v1.tmp" })
        { string path = Path.Combine(expected, name); if (File.Exists(path)) File.Delete(path); }
    }
    sealed class Fixture : IDisposable
    {
        public readonly Control Ui = new Control();
        public readonly Runner Runner = new Runner();
        public readonly AppSettings Cfg;
        public readonly BindingList<Product> Products;
        public readonly AutomationController Auto;
        public string Notice = "";
        int workerFinished, uiFinished;
        public Fixture(int count, AppSettings cfg = null, bool registered = false, Action beforeFinished = null)
        {
            ResetLedger(); Bot.Reset(); ServerClock.Now = new DateTime(2026, 10, 26, 15, 2, 0); ServerClock.FreshValue = true; Seed();
            Cfg = cfg ?? Batch(); Products = Items(count, registered); Ui.CreateControl();
            if (beforeFinished != null) Runner.Finished += beforeFinished;
            Auto = new AutomationController(Cfg, new SelectorSet(), Products, Runner, Ui);
            Auto.Notice = (message, title) => Notice = message;
            // Controller의 완료 콜백 뒤에 표식을 넣어 집계와 예약 갱신까지 기다린다.
            Runner.Finished += () => { Interlocked.Increment(ref workerFinished); Ui.BeginInvoke((Action)(() => uiFinished++)); };
            currentFixture = this;
        }
        public bool RoundCompleted
        {
            get
            {
                var thread = (Thread)typeof(Runner).GetField("_thread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Runner);
                return !Runner.Running && (thread == null || !thread.IsAlive) && uiFinished == Interlocked.CompareExchange(ref workerFinished, 0, 0);
            }
        }
        public string Snapshot(string name)
        {
            try
            {
                var thread = (Thread)typeof(Runner).GetField("_thread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Runner);
                return new JObject {
                    { "check", name }, { "attempts", Bot.Attempts.Count }, { "running", Runner.Running }, { "active", Auto.Active },
                    { "round", Auto.Round }, { "nextRun", Auto.NextRun.ToString("O") }, { "notice", Notice }, { "blockedReason", Runner.BlockedReason },
                    { "now", ServerClock.Now.ToString("O") }, { "fresh", ServerClock.Fresh }, { "paused", Auto.Paused }, { "state", Auto.StateText },
                    { "success", Auto.SuccessThisRun }, { "failure", Auto.FailThisRun }, { "lastRegistered", Runner.LastRegistered.Count },
                    { "threadAlive", thread != null && thread.IsAlive }, { "threadState", thread == null ? "없음" : thread.ThreadState.ToString() },
                    { "uiHandle", Ui.IsHandleCreated }, { "uiDisposed", Ui.IsDisposed }, { "edition", LicenseGate.LastInfo.Edition }, { "allowed", LicenseGate.Allowed },
                    { "runnerError", Log.LastError }, { "quotaError", Bot.LastQuotaError }, { "ledger", LedgerSnapshot() }
                }.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex) { return "진단 실패: " + ex; }
        }
        public void StartAndWait()
        {
            Auto.Start();
            // 전체 목록의 실제 완료와 UI 콜백은 짧은 상태 반응 검사보다 오래 걸릴 수 있다.
            Check("회차 종료 응답", Until(() => RoundCompleted && (Auto.NextRun != DateTime.MinValue || !Auto.Active), 10000));
        }
        public void Dispose()
        {
            Auto.Stop(); Runner.Shutdown();
            bool drained = Until(() => RoundCompleted);
            Check("검사 종료 시 작업 스레드와 UI 콜백 완료", drained);
            if (!drained) throw new InvalidOperationException("검사 작업이 남아 있어 다음 검사 상태를 초기화할 수 없습니다.");
            Ui.Dispose(); currentFixture = null;
        }
    }
    static JObject LedgerSnapshot()
    {
        var result = new JObject();
        foreach (string copy in new[] { "file", "registry" })
        {
            try
            {
                byte[] bytes = null;
                if (copy == "file") { string path = Path.Combine(Paths.Root, "usage.v1"); if (File.Exists(path)) bytes = File.ReadAllBytes(path); }
                else using (var key = Registry.CurrentUser.OpenSubKey(@"Software\EXP\Tests\" + Process.GetCurrentProcess().Id)) if (key != null) bytes = key.GetValue("State") as byte[];
                if (bytes == null) { result[copy] = "없음"; continue; }
                var state = (JObject)typeof(UsageQuota).GetMethod("Decode", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { bytes });
                result[copy] = new JObject { { "seq", state["seq"] }, { "used", state["used"] }, { "day", state["day"] }, { "lastUtc", state["lastUtc"] }, { "previous", state["previous"] }, { "bytes", bytes.Length } };
            }
            catch (Exception ex) { result[copy] = ex.ToString(); }
        }
        return result;
    }
    static void Time(string name, DateTime anchor, DateTime finish, bool aligned, int gap, int minute, DateTime expected)
    {
        var type = typeof(Runner).Assembly.GetType("BunjangAuto.BatchRepeatSchedule");
        var method = type == null ? null : type.GetMethod("NextAfter");
        Check(name, method != null && (DateTime)method.Invoke(null, new object[] { anchor, finish, aligned, gap, minute }) == expected);
    }
    static void ScheduleChecks()
    {
        var start = new DateTime(2026, 10, 26, 15, 2, 0);
        Time("60분은 시작 시각 15:02 기준", start, start.AddMinutes(1), false, 60, 0, new DateTime(2026, 10, 26, 16, 2, 0));
        Time("매시 0분", start, start.AddMinutes(1), true, 60, 0, new DateTime(2026, 10, 26, 16, 0, 0));
        Time("매시 20분", start, start.AddMinutes(1), true, 60, 20, new DateTime(2026, 10, 26, 15, 20, 0));
        Time("지정분에 끝나면 다음 미래 시간", start, new DateTime(2026, 10, 26, 16, 20, 0), true, 60, 20, new DateTime(2026, 10, 26, 17, 20, 0));
        Time("느린 회차의 지난 간격 건너뜀", start, new DateTime(2026, 10, 26, 18, 12, 0), false, 60, 0, new DateTime(2026, 10, 26, 19, 2, 0));
        Time("간격 경계와 같아도 미래 슬롯", start, new DateTime(2026, 10, 26, 16, 2, 0), false, 60, 0, new DateTime(2026, 10, 26, 17, 2, 0));
        Time("날짜 경계 간격", new DateTime(2026, 10, 26, 23, 32, 0), new DateTime(2026, 10, 26, 23, 50, 0), false, 60, 0, new DateTime(2026, 10, 27, 0, 32, 0));
        Time("날짜 경계 지정분", new DateTime(2026, 10, 26, 23, 2, 0), new DateTime(2026, 10, 26, 23, 59, 59), true, 60, 0, new DateTime(2026, 10, 27, 0, 0, 0));
        Time("1440분 간격", start, start.AddMinutes(1), false, 1440, 0, new DateTime(2026, 10, 27, 15, 2, 0));
    }
    static void WholeList(int count)
    {
        using (var f = new Fixture(count, null, true))
        {
            f.StartAndWait();
            bool ordered = Bot.Attempts.SequenceEqual(f.Products.Select(p => p.Title));
            bool state = count == 0 ? !f.Auto.Active : f.Auto.Active && f.Auto.NextRun == new DateTime(2026, 10, 26, 16, 2, 0);
            if (!ordered || !state || selection.Length > 0)
            {
                Console.WriteLine("목록 진단 " + new JObject {
                    { "count", count }, { "attempts", Bot.Attempts.Count }, { "running", f.Runner.Running }, { "active", f.Auto.Active },
                    { "notice", f.Notice }, { "blockedReason", f.Runner.BlockedReason }, { "nextRun", f.Auto.NextRun.ToString("O") },
                    { "lastRegistered", f.Runner.LastRegistered.Count }, { "success", f.Auto.SuccessThisRun }, { "failure", f.Auto.FailThisRun },
                    { "edition", LicenseGate.LastInfo.Edition }, { "quota", UsageQuota.Summary() }, { "fixtureRoot", Paths.Root },
                    { "ledgerFile", File.Exists(Path.Combine(Paths.Root, "usage.v1")) }, { "registry", @"Software\EXP\Tests\" + Process.GetCurrentProcess().Id }
                }.ToString(Newtonsoft.Json.Formatting.None));
            }
            Check(count + "개 전체 목록 첫 회차 기존 상품 포함", ordered);
            Check(count + "개 회차 상태", state);
        }
    }
    static void RunChecks()
    {
        foreach (int count in new[] { 0, 31, 65 }) WholeList(count);
        using (var f = new Fixture(2))
        {
            f.Products.Add(new Product { Title = "가격 없는 상품" });
            f.StartAndWait(); Check("유효하지 않은 상품 제외", Bot.Attempts.Count == 2);
            f.Cfg.OneByOne = true; Set(f.Cfg, "BatchRepeatMinutes", 1); Set(f.Cfg, "BatchRepeatEnabled", false);
            ServerClock.Now = new DateTime(2026, 10, 26, 16, 2, 0); f.Auto.Tick();
            Check("설정 변경에도 시작 설정으로 전체 회차", Until(() => f.RoundCompleted && Bot.Attempts.Count == 4));
            Check("설정 동결의 다음 60분 슬롯", f.Auto.NextRun == new DateTime(2026, 10, 26, 17, 2, 0));
        }
        using (var f = new Fixture(1, Batch(true, 60, 20)))
        {
            f.StartAndWait(); Check("Controller 지정분 연결", f.Auto.NextRun == new DateTime(2026, 10, 26, 15, 20, 0));
            f.Auto.Pause(); ServerClock.Now = new DateTime(2026, 10, 26, 16, 40, 0); f.Auto.Tick();
            Check("대기 중 일시정지로 회차 방지", f.Auto.Paused && Bot.Attempts.Count == 1);
            f.Auto.Resume(); f.Auto.Tick(); Check("일시정지 후 지연 회차 한 번만", Until(() => Bot.Attempts.Count == 2 && f.RoundCompleted));
            Check("지연 회차 후 몰아 실행 금지", f.Auto.NextRun == new DateTime(2026, 10, 26, 17, 20, 0));
            f.Auto.Stop(); ServerClock.Now = new DateTime(2026, 10, 26, 18, 0, 0); f.Auto.Tick();
            Check("예약 중지 후 재시작 없음", !f.Auto.Active && Bot.Attempts.Count == 2);
        }
        using (var f = new Fixture(2))
        {
            f.StartAndWait(); ServerClock.FreshValue = false; ServerClock.Now = new DateTime(2026, 10, 26, 17, 0, 0); f.Auto.Tick();
            Check("서버 시각 오래되면 예약 게시 금지", Bot.Attempts.Count == 2 && f.Auto.Active);
            ServerClock.FreshValue = true; f.Auto.Tick(); Check("시각 복구 후 한 회차", Until(() => Bot.Attempts.Count == 4 && f.RoundCompleted));
            Check("시각 복구 뒤 미래 슬롯", f.Auto.NextRun == new DateTime(2026, 10, 26, 17, 2, 0));
        }
        using (var f = new Fixture(1))
        {
            ServerClock.FreshValue = false; f.Auto.Start(); Check("시각 미확인 시작 차단", !f.Auto.Active && Bot.Attempts.Count == 0 && f.Notice.Length > 0);
        }
        foreach (int invalid in new[] { 0, 1441 }) using (var f = new Fixture(1, Batch(false, invalid)))
        { f.Auto.Start(); Check("잘못된 간격 " + invalid + " 차단", !f.Auto.Active && f.Notice.Length > 0); }
        foreach (int invalid in new[] { -1, 60 }) using (var f = new Fixture(1, Batch(true, 60, invalid)))
        { f.Auto.Start(); Check("잘못된 지정분 " + invalid + " 차단", !f.Auto.Active && f.Notice.Length > 0); }
        using (var f = new Fixture(2))
        {
            LicenseGate.LastInfo.Edition = "Classic"; f.Auto.Start();
            Check("Classic은 첫 전체 회차부터 실제 RequireMode 차단", !f.Auto.Active && Bot.Attempts.Count == 0 && f.Notice.Length > 0);
        }
        using (var f = new Fixture(3))
        {
            Seed(99); f.StartAndWait(); Check("실제 Pro 한도 소진 후 회차와 예약 중단", Bot.Attempts.Count == 1 && !f.Auto.Active && f.Notice.Length > 0);
        }
        using (var f = new Fixture(1))
        {
            f.StartAndWait(); Bot.LoginOk = false; ServerClock.Now = f.Auto.NextRun; f.Auto.Tick();
            Check("로그인 실패도 종료 응답", Until(() => !f.Auto.Active && f.RoundCompleted));
            Check("로그인 실패에 낡은 시도 재집계 없음", f.Auto.SuccessThisRun == 1 && f.Runner.LastRegistered.Count == 0 && f.Notice.Length > 0);
        }
        using (var f = new Fixture(3))
        {
            Bot.FailTitle = "상품1"; f.StartAndWait();
            Check("일부 실패에도 남은 상품 시도", Bot.Attempts.Count == 3 && f.Auto.SuccessThisRun == 2 && f.Auto.FailThisRun == 1);
            Check("실패 회차 뒤 무한 반복 차단", !f.Auto.Active && f.Notice.Length > 0);
        }
        using (var f = new Fixture(3))
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false);
            Bot.BeforeRegister = p => { entered.Set(); release.WaitOne(1500); };
            f.Auto.Start(); Check("진행 중 진입", entered.WaitOne(1000)); f.Auto.Start();
            Check("중복 Start가 새 Runner/집계 초기화 안 함", Bot.Created == 1 && f.Auto.Active);
            f.Auto.Pause(); release.Set(); Thread.Sleep(20); Application.DoEvents();
            Check("실행 중 일시정지", f.Auto.Paused && f.Runner.Running && Bot.Attempts.Count == 0);
            f.Auto.Stop(); Check("일시정지 중 중지 반응", Until(() => !f.Auto.Active && f.RoundCompleted));
            Check("중지 후 남은 상품 게시 없음", Bot.Attempts.Count == 0); entered.Dispose(); release.Dispose();
        }
        using (var f = new Fixture(2))
        {
            Bot.BeforeRegister = p => ServerClock.Now = new DateTime(2026, 10, 26, 18, 12, 0);
            f.StartAndWait(); Check("느린 실제 회차 뒤 다음 미래 슬롯", f.Auto.NextRun == new DateTime(2026, 10, 26, 19, 2, 0));
        }
        using (var f = new Fixture(2))
        {
            f.StartAndWait(); DateTime next = f.Auto.NextRun; f.Auto.Start();
            Check("예약 대기 중 중복 시작 무시", f.Auto.NextRun == next && f.Auto.SuccessThisRun == 2 && Bot.Attempts.Count == 2);
            for (int i = 0; i < 3; i++)
            {
                ServerClock.Now = f.Auto.NextRun; f.Auto.Tick(); int expected = 4 + i * 2;
                Check("지속 반복 회차 " + (i + 2), Until(() => f.RoundCompleted && Bot.Attempts.Count == expected && f.Auto.NextRun > ServerClock.Now));
            }
            Check("기존 횟수 제한 없이 계속 반복", f.Auto.Active && f.Auto.SuccessThisRun == 8);
        }
        using (var f = new Fixture(2, new AppSettings { OneByOne = true, RepeatAll = true, RepeatCount = 1 }))
        {
            Set(f.Cfg, "BatchRepeatEnabled", true); f.StartAndWait();
            Check("전체 모드가 레거시 1개씩과 횟수보다 우선", Bot.Attempts.Count == 2 && f.Auto.Active);
        }
        using (var f = new Fixture(1))
        {
            f.StartAndWait(); LicenseGate.Allowed = false; ServerClock.Now = f.Auto.NextRun; f.Auto.Tick();
            Check("다음 회차 인증 해제 시 즉시 중단", !f.Auto.Active && Bot.Attempts.Count == 1 && f.Notice.Length > 0);
        }
        using (var f = new Fixture(1))
        {
            Bot.BeforeRegister = p => { throw new IOException("제어된 브라우저 오류"); }; f.StartAndWait();
            Check("회차 예외는 안내 후 반복 중단", !f.Auto.Active && Bot.Attempts.Count == 0 && f.Notice.Contains("제어된 브라우저 오류"));
        }
        using (var f = new Fixture(1))
        {
            Bot.AfterPause = () => ServerClock.FreshValue = false; f.Auto.Start();
            Check("종료 시 시각 만료 응답", Until(() => f.RoundCompleted && f.Auto.NextText.Contains("서버 시각")));
            Check("회차 종료 후 미확인 시각으로 예약 안 함", f.Auto.Active && f.Auto.NextRun == DateTime.MinValue);
            ServerClock.Now = new DateTime(2026, 10, 26, 17, 3, 0); ServerClock.FreshValue = true; f.Auto.Tick();
            Check("회차 종료 시각 복구 뒤 미래 슬롯 재계산", f.Auto.NextRun == new DateTime(2026, 10, 26, 18, 2, 0) && Bot.Attempts.Count == 1);
        }
        using (var f = new Fixture(1))
        {
            ServerClock.Now = new DateTime(2026, 10, 26, 23, 59, 0); Seed(99); f.StartAndWait();
            ServerClock.Now = new DateTime(2026, 10, 27, 0, 59, 0); f.Auto.Tick();
            Check("날짜 경계 실제 일일한도 초기화로 다음 회차", Until(() => f.RoundCompleted && f.Auto.SuccessThisRun == 2 && f.Auto.Active));
        }
        using (var f = new Fixture(1))
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false);
            Bot.BeforeCreate = () => { entered.Set(); release.WaitOne(1000); };
            f.Auto.Start(); Check("브라우저 생성 진입", entered.WaitOne(1000)); f.Auto.Stop(); release.Set();
            Check("브라우저 생성 중 중지 보존", Until(() => !f.Auto.Active && f.RoundCompleted) && Bot.Attempts.Count == 0);
            entered.Dispose(); release.Dispose();
        }
        using (var f = new Fixture(3, new AppSettings { OneByOne = false }))
        { f.StartAndWait(); Check("레거시 일괄 1회 종료", Bot.Attempts.Count == 3 && !f.Auto.Active); }
        using (var f = new Fixture(31, new AppSettings { OneByOne = false }))
        { f.StartAndWait(); Check("레거시 일괄 최대30 보존", Bot.Attempts.Count == 30 && !f.Auto.Active); }
        using (var f = new Fixture(2, new AppSettings { OneByOne = true, OneByOneMinutes = 20, RepeatAll = true, RepeatCount = 2 }))
        {
            f.StartAndWait(); for (int i = 0; i < 3; i++) { ServerClock.Now = f.Auto.NextRun; f.Auto.Tick(); Check("레거시 다음 상품 " + i, Until(() => f.RoundCompleted)); }
            Check("레거시 횟수 반복 완료", Bot.Attempts.Count == 4 && !f.Auto.Active);
        }
        using (var f = new Fixture(1, new AppSettings { OneByOne = true, UseTimeRules = true, TimeRules = new List<TimeRule> { new TimeRule("16:00", "17:00", 20) } }))
        {
            f.Auto.Start(); Check("레거시 시간대 대기 보존", f.Auto.Active && Bot.Attempts.Count == 0 && f.Auto.NextRun == new DateTime(2026, 10, 26, 16, 0, 0));
        }
    }
    static AppSettings Window(bool aligned = true, string start = "18:00", string end = "24:00", int minutes = 60)
    {
        var cfg = Batch(aligned, minutes); cfg.BatchRepeatWindowEnabled = true;
        cfg.BatchRepeatWindowStart = start; cfg.BatchRepeatWindowEnd = end; return cfg;
    }
    static void ImmediateChecks()
    {
        var date = new DateTime(2026, 10, 26);
        using (var f = new Fixture(2, Window()))
        {
            Set(f.Cfg, "BatchRepeatImmediateOnStart", true); ServerClock.Now = date.AddHours(17).AddMinutes(30); f.Auto.Start();
            Check("옵션 ON 17:30 전체 첫 회차 즉시", Until(() => f.RoundCompleted && Bot.Attempts.Count == 2));
            Check("첫 즉시 뒤18시 예약", f.Auto.NextRun == date.AddHours(18));
            ServerClock.Now = date.AddHours(18); f.Auto.Tick(); Check("즉시옵션 두번째 회차18시 실행", Until(() => f.RoundCompleted && Bot.Attempts.Count == 4));
            Bot.BeforeRegister = p => ServerClock.Now = date.AddDays(1); ServerClock.Now = date.AddHours(23); f.Auto.Tick();
            Check("첫 회차 면제가 이후회차 상품으로 전파 안 됨", Until(() => f.RoundCompleted && f.Auto.NextRun == date.AddDays(1).AddHours(18)) && Bot.Attempts.Count == 5);
        }
        using (var f = new Fixture(2, Window()))
        {
            f.Cfg.BatchRepeatImmediateOnStart = true; ServerClock.Now = date.AddHours(17).AddMinutes(30); Bot.BeforeRegister = p => ServerClock.Now = date.AddDays(1); f.Auto.Start();
            Check("명시 첫회차 전체목록 시간대밖 허용", Until(() => f.RoundCompleted && Bot.Attempts.Count == 2 && f.Auto.NextRun == date.AddDays(1).AddHours(18)));
        }
        using (var f = new Fixture(1, Window()))
        {
            f.Cfg.BatchRepeatImmediateOnStart = true; LicenseGate.LastInfo.Edition = "Classic"; f.Auto.Start(); Check("즉시옵션 Classic 권한 유지", !f.Auto.Active && Bot.Attempts.Count == 0 && f.Notice.Length > 0);
        }
        using (var f = new Fixture(2, Window()))
        {
            f.Cfg.BatchRepeatImmediateOnStart = true; ServerClock.Now = date.AddHours(17).AddMinutes(30); Seed(99); f.Auto.Start();
            Check("즉시옵션 일일한도 유지", Until(() => f.RoundCompleted && !f.Auto.Active) && Bot.Attempts.Count == 1);
        }
        using (var f = new Fixture(1, Window()))
        {
            f.Cfg.BatchRepeatImmediateOnStart = true; ServerClock.FreshValue = false; f.Auto.Start(); Check("즉시옵션 서버시각 필수", !f.Auto.Active && Bot.Attempts.Count == 0 && f.Notice.Length > 0);
        }
    }
    static void WindowChecks()
    {
        var date = new DateTime(2026, 10, 26);
        var cfg = Window(); string error;
        Check("구버전 기본 시간 제한 OFF와 입력값", !new AppSettings().BatchRepeatWindowEnabled && new AppSettings().BatchRepeatWindowStart == "18:00" && new AppSettings().BatchRepeatWindowEnd == "24:00");
        Check("18시 시작 포함", BatchRepeatSchedule.IsAllowed(cfg, date.AddHours(18)));
        Check("24시 종료 제외", !BatchRepeatSchedule.IsAllowed(cfg, date.AddDays(1)));
        Check("종료 직전 포함", BatchRepeatSchedule.IsAllowed(cfg, date.AddDays(1).AddTicks(-1)));
        foreach (var pair in new[] { new[] { "24:00", "02:00" }, new[] { "18:00", "24:01" }, new[] { "18:00", "18:00" }, new[] { "8:00", "24:00" }, new[] { "18:00", "invalid" } })
            Check("잘못된 시간 범위 차단 " + pair[0] + "/" + pair[1], !BatchRepeatSchedule.TryValidate(Window(true, pair[0], pair[1]), out error));
        var night = Window(true, "22:00", "02:00");
        Check("야간 자정 포함", BatchRepeatSchedule.IsAllowed(night, date.AddDays(1)));
        Check("야간 종료 제외", !BatchRepeatSchedule.IsAllowed(night, date.AddDays(1).AddHours(2)));
        Check("야간 다음 22시", BatchRepeatSchedule.NextBatch(night, date, date.AddDays(1).AddHours(1)) == date.AddDays(1).AddHours(22));
        Check("간격 anchor 유지 17:32 → 18:32", BatchRepeatSchedule.AtOrAfter(Window(false), date.AddHours(17).AddMinutes(32), date.AddHours(17).AddMinutes(32)) == date.AddHours(18).AddMinutes(32));
        Check("간격 자정 제외 후 다음날 anchor 유지", BatchRepeatSchedule.NextBatch(Window(false), date.AddHours(17).AddMinutes(32), date.AddHours(23).AddMinutes(50)) == date.AddDays(1).AddHours(18).AddMinutes(32));
        foreach (var begin in new[] { date.AddHours(17).AddMinutes(30), date.AddHours(18).AddMinutes(2), date.AddHours(18) })
        using (var f = new Fixture(1, Window()))
        {
            ServerClock.Now = begin; f.Auto.Start();
            if (begin == date.AddHours(18)) Check("18시 정각 첫 회차 즉시", Until(() => f.RoundCompleted && Bot.Attempts.Count == 1));
            else Check("시간 제한 첫 슬롯 " + begin.ToString("HH:mm"), f.Auto.NextRun == (begin.Hour == 17 ? date.AddHours(18) : date.AddHours(19)) && Bot.Attempts.Count == 0);
        }
        using (var f = new Fixture(1, Window()))
        {
            ServerClock.Now = date.AddHours(17).AddMinutes(30); f.Auto.Start();
            // 실행 중 원본 설정을 바꾸어도 시작 시점의 제한과 지정분을 유지한다.
            f.Cfg.BatchRepeatWindowEnabled = false; f.Cfg.BatchRepeatMinuteOfHour = 20;
            for (int hour = 18; hour <= 23; hour++)
            { ServerClock.Now = date.AddHours(hour); f.Auto.Tick(); int expected = hour - 17; Check(hour + "시 전체 목록 실행", Until(() => f.RoundCompleted && Bot.Attempts.Count == expected)); }
            Check("23시 뒤 다음날18시 예약", f.Auto.NextRun == date.AddDays(1).AddHours(18));
            ServerClock.Now = date.AddDays(1); f.Auto.Tick(); Check("24시 새 회차 없음", Bot.Attempts.Count == 6);
            f.Auto.Stop(); ServerClock.Now = date.AddDays(1).AddHours(18); f.Auto.Tick(); Check("중지 후 다음날 실행 없음", !f.Auto.Active && Bot.Attempts.Count == 6);
        }
        foreach (bool aligned in new[] { true, false })
        using (var f = new Fixture(1, Window(aligned)))
        {
            ServerClock.Now = date.AddHours(17); f.Auto.Start(); ServerClock.Now = date.AddHours(18).AddSeconds(1); f.Auto.Tick();
            Check("정상 Tick 같은 예정분 내 지연 허용 " + aligned, Until(() => f.RoundCompleted && Bot.Attempts.Count == 1));
            ServerClock.Now = date.AddHours(19).AddMinutes(1); f.Auto.Tick();
            Check("예정분 넘은 Tick 다음 슬롯 " + aligned, Bot.Attempts.Count == 1 && f.Auto.NextRun == date.AddHours(20));
        }
        using (var f = new Fixture(2, Window()))
        {
            ServerClock.Now = date.AddHours(23); Bot.BeforeRegister = p => ServerClock.Now = date.AddDays(1);
            f.Auto.Start(); Check("진행 상품 마무리 후 종료 경계 새상품 차단", Until(() => f.RoundCompleted && f.Auto.NextRun == date.AddDays(1).AddHours(18)) && Bot.Attempts.Count == 1 && f.Runner.WindowDeferred);
        }
        using (var f = new Fixture(1, Window()))
        {
            ServerClock.Now = date.AddHours(17).AddMinutes(30); f.Auto.Start(); f.Auto.Pause();
            ServerClock.Now = date.AddHours(18).AddMinutes(2); f.Auto.Resume(); f.Auto.Tick();
            Check("일시정지 복귀 지난 지정분 건너뜀", Bot.Attempts.Count == 0 && f.Auto.NextRun == date.AddHours(19));
            ServerClock.FreshValue = false; ServerClock.Now = date.AddHours(19); f.Auto.Tick(); ServerClock.Now = date.AddHours(19).AddMinutes(2); ServerClock.FreshValue = true; f.Auto.Tick();
            Check("시각 복구 지난 지정분 건너뜀", Bot.Attempts.Count == 0 && f.Auto.NextRun == date.AddHours(20));
            ServerClock.Now = date.AddDays(1).AddHours(1); f.Auto.Tick(); Check("지연 Tick 시간밖 실행 금지", Bot.Attempts.Count == 0 && f.Auto.NextRun == date.AddDays(1).AddHours(18));
        }
        using (var f = new Fixture(2, Window()))
        {
            ServerClock.Now = date.AddHours(18); Bot.AfterPause = () => ServerClock.FreshValue = false;
            f.Auto.Start(); Check("상품 사이 시각 미확인 새상품 차단", Until(() => f.RoundCompleted && f.Auto.NextText.Contains("서버 시각")) && Bot.Attempts.Count == 1);
            ServerClock.Now = date.AddDays(1).AddHours(2); ServerClock.FreshValue = true; f.Auto.Tick(); Check("상품중 시각복구 시간밖 대기", Bot.Attempts.Count == 1 && f.Auto.NextRun == date.AddDays(1).AddHours(18));
        }
        using (var f = new Fixture(2, Window()))
        using (var entered = new ManualResetEvent(false))
        using (var release = new ManualResetEvent(false))
        {
            ServerClock.Now = date.AddHours(23); Bot.AfterPause = () => { entered.Set(); release.WaitOne(1000); };
            f.Auto.Start(); Check("상품 사이 일시정지 지점 진입", entered.WaitOne(1000)); f.Auto.Pause(); release.Set();
            ServerClock.Now = date.AddDays(1); f.Auto.Resume();
            Check("실행 중 일시정지 복귀도 다음상품 시간검사", Until(() => f.RoundCompleted && f.Auto.NextRun == date.AddDays(1).AddHours(18)) && Bot.Attempts.Count == 1);
        }
    }
    static void SlowCompletionChecks()
    {
        using (var f = new Fixture(1))
        {
            // 완료 신호를 기다리는 검사이며 제품 성능의 제한 시간을 정의하지 않는다.
            Bot.BeforeCreate = () => Thread.Sleep(2100);
            f.StartAndWait();
            Check("제어된 2.1초 지연 뒤 목록과 다음 슬롯 보존", f.RoundCompleted && Bot.Attempts.Count == 1 && f.Auto.NextRun == new DateTime(2026, 10, 26, 16, 2, 0));
        }
    }
    static void CompletionSignalChecks()
    {
        using (var entered = new ManualResetEvent(false))
        using (var release = new ManualResetEvent(false))
        using (var fixture = new Fixture(1, null, false, () => { entered.Set(); release.WaitOne(1000); }))
        {
            fixture.Auto.Start();
            try
            {
                Check("제어된 Finished 지연 진입", entered.WaitOne(1000));
                Check("Running 해제와 UI 회차 완료는 다른 시점", Until(() => !fixture.Runner.Running) && fixture.Auto.NextRun == DateTime.MinValue && fixture.Auto.Active);
                Check("UI 콜백 전 회차 완료로 판단하지 않음", !fixture.RoundCompleted);
            }
            finally { release.Set(); }
            Check("지연 해제 후 실제 다음 예약 생성", Until(() => fixture.Auto.NextRun == new DateTime(2026, 10, 26, 16, 2, 0)));
        }
    }
    [STAThread] static int Main(string[] args)
    {
        selection = args.Length == 0 ? "" : args[0];
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) { using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("lib." + new AssemblyName(e.Name).Name + ".dll")) { if (s == null) return null; using (var m = new MemoryStream()) { s.CopyTo(m); return Assembly.Load(m.ToArray()); } } };
        return Execute();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Execute()
    {
        try
        {
            if (selection == "--whole65") WholeList(65);
            else if (selection == "--whole-lists") foreach (int count in new[] { 0, 31, 65 }) WholeList(count);
            else if (selection == "--immediate") ImmediateChecks();
            else if (selection == "--window") WindowChecks();
            else if (selection == "--slow-completion") SlowCompletionChecks();
            else if (selection == "--completion-signal") CompletionSignalChecks();
            else { ScheduleChecks(); RunChecks(); WindowChecks(); ImmediateChecks(); }
        }
        catch (Exception ex) { Console.WriteLine(ex); failed++; }
        finally
        {
            ResetLedger();
        }
        Console.WriteLine("BATCH: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }
}

namespace BunjangAuto
{
    // 아래 대역은 이 격리 검사 실행 파일에만 포함되며 운영 앱에는 포함되지 않는다.
    public class SelectorSet { }
    public class StopRequestedException : Exception { }
    public class RegistrationBlockedException : Exception { public RegistrationBlockedException(string message) : base(message) { } }
    public class LicenseInfo { public bool Valid = true, Trial; public string Edition = "Pro", MachineId = "isolated-device", LicenseId = "isolated-license"; }
    public static class LicenseGate { public static bool Allowed = true; public static LicenseInfo LastInfo = new LicenseInfo(); public static bool Recheck() { return Allowed; } }
    public static class Guard { public static void Check() { if (!LicenseGate.Recheck()) throw new RegistrationBlockedException("격리 검사 인증 차단"); } }
    public static class Brand { public const string Email = "isolated@example.invalid"; }
    public static class TrustedKeys { public static IDictionary<string, byte[]> All = new Dictionary<string, byte[]>(); }
    public static class TpmDevice { public static void Prove(string key) { throw new InvalidOperationException("이 검사에서는 기기 인증을 수행하지 않습니다."); } }
    public static class Storage { public static void SaveSettings(AppSettings cfg) { } public static void SaveProducts(BindingList<Product> products) { } }
    public static class Log { public static string LastError = ""; public static void Write(string message) { } public static void Write(string a, string b, string c, string d, string e) { } public static void Error(string a, string b, string c, Exception ex) { LastError = ex.ToString(); } }
    public static class ServerClock
    {
        public static DateTime Now = new DateTime(2026, 10, 26, 15, 2, 0); public static bool FreshValue = true;
        public static DateTime KoreaNow { get { return Now; } } public static DateTime UtcNow { get { return DateTime.SpecifyKind(Now.AddHours(-9), DateTimeKind.Utc); } }
        public static bool Fresh { get { return FreshValue; } } public static void RefreshAsync() { }
        public static void RequireFresh() { if (!Fresh) throw new InvalidOperationException("격리 검사 서버 시각 미확인"); }
    }
    public class Bot
    {
        public static bool LoginOk = true; public static int Created; public static string FailTitle = "";
        public static List<string> Attempts = new List<string>(); public static Action<Product> BeforeRegister;
        public static Action BeforeCreate, AfterPause;
        public static string LastQuotaError = "";
        public volatile bool StopRequested, PauseRequested;
        public Func<string, bool> Ask; public Action<string> NotifyManual;
        public bool IsAlive { get { return true; } }
        public Bot(AppSettings cfg, SelectorSet selectors) { if (BeforeCreate != null) BeforeCreate(); Created++; }
        public static void Reset() { LoginOk = true; Created = 0; FailTitle = ""; Attempts.Clear(); BeforeRegister = null; BeforeCreate = null; AfterPause = null; Log.LastError = ""; LastQuotaError = ""; }
        public bool EnsureLogin() { return LoginOk; }
        void CheckStop() { while (PauseRequested && !StopRequested) Thread.Sleep(2); if (StopRequested) throw new StopRequestedException(); }
        public bool Register(Product product)
        {
            if (BeforeRegister != null) BeforeRegister(product); CheckStop();
            try { UsageQuota.Consume(); } catch (Exception ex) { LastQuotaError = "HResult=" + ex.HResult + "\n" + ex.ToString(); throw new RegistrationBlockedException(ex.Message); }
            Attempts.Add(product.Title); product.Status = product.Title == FailTitle ? "실패: 제어된 상품 오류" : "등록됨"; return product.Status == "등록됨";
        }
        public void HumanPause() { CheckStop(); if (AfterPause != null) AfterPause(); } public void Close() { }
    }
}
