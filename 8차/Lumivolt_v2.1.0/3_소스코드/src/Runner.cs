using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace BunjangAuto
{
    /// <summary>한 번의 실행(로그인 → 등록)을 백그라운드 스레드에서 수행한다.</summary>
    public class Runner
    {
        private Thread _thread;
        private Bot _bot;
        private readonly object _gate = new object();

        public bool Running { get; private set; }
        public event Action Finished;
        public event Action<int, int> Progress;      // (완료, 전체)
        public event Action<string> ManualLoginNeeded;
        public Func<string, bool> Ask;

        /// <summary>'1개씩 등록'을 끈 일괄 모드에서 한 번에 등록할 최대 개수</summary>
        private const int BatchLimit = 30;

        public Bot CurrentBot { get { return _bot; } }

        /// <summary>일시정지 중인지 (v2)</summary>
        public bool Paused { get { return _bot != null && _bot.PauseRequested; } }
        /// <summary>지금 등록 중인 상품(화면 표시용)</summary>
        public volatile Product CurrentProduct;

        public void Pause()
        {
            if (_bot != null && Running && !_bot.PauseRequested)
            {
                _bot.PauseRequested = true;
                Log.Write("INFO", "Automation", "run.pause", "[일시정지] 현재 단계가 끝나는 대로 멈춥니다.", "");
            }
        }

        public void Resume()
        {
            if (_bot != null && _bot.PauseRequested)
            {
                _bot.PauseRequested = false;
                Log.Write("INFO", "Automation", "run.resume", "[일시정지 해제] 이어서 진행합니다.", "");
            }
        }
        /// <summary>이번 실행에서 등록을 시도한 상품(같은 회차 안에서 다음 실행 때 제외하기 위함)</summary>
        public List<Product> LastRegistered = new List<Product>();

        /// <summary>브라우저를 계속 열어두고 재사용한다(예약 반복 실행 시 로그인 유지).</summary>
        /// <param name="includeRegistered">true 이면 이미 등록된 상품도 목록 순서대로 다시 등록한다(중복 허용 / 2회차 이후 반복).</param>
        public void Run(AppSettings cfg, SelectorSet sel, BindingList<Product> products,
                        Func<IList<Product>> snapshot, Action saveProducts, bool includeRegistered)
        {
            Guard.Check();
            lock (_gate)
            {
                if (Running) return;
                Running = true;
            }
            if (_bot == null) _bot = new Bot(cfg, sel);
            _bot.StopRequested = false;
            _bot.PauseRequested = false;
            _bot.Ask = Ask;
            _bot.NotifyManual = delegate (string m) { var h = ManualLoginNeeded; if (h != null) h(m); };

            var bot = _bot;

            _thread = new Thread(delegate ()
            {
                try
                {
                    Guard.Check();
                    Log.Write("===== 작업 시작 =====");
                    if (!bot.EnsureLogin()) { Log.Write("로그인에 실패해 작업을 중단합니다."); return; }

                    var list = snapshot();
                    int limit = BatchLimit;
                    int regLimit = cfg.OneByOne ? 1 : limit;   // 1개씩 등록 모드면 한 번에 1개만
                    int total = 0, done = 0;

                    var toRegister = new List<Product>();
                    if (cfg.DoRegister)
                        toRegister = (includeRegistered
                            ? list.Where(p => p.IsValid)
                            : list.Where(p => p.IsValid && string.IsNullOrEmpty(p.ProductId) && p.Status != "등록됨")).Take(regLimit).ToList();
                    LastRegistered = new List<Product>(toRegister);
                    total = toRegister.Count;
                    Report(0, total);

                    foreach (var p in toRegister)
                    {
                        Guard.Check();
                        p.Status = "진행중";
                        CurrentProduct = p;
                        bot.Register(p);
                        CurrentProduct = null;
                        if (p.Status == "등록됨")
                        {
                            // 등록이 끝까지 성공했다는 것은 이미지 업로드/카테고리 선택도 성공했다는 뜻이므로 구조화 이벤트만 추가로 남긴다
                            if (!string.IsNullOrWhiteSpace(p.Images)) Log.Write("DEBUG", "ProductUpload", "image.upload.success", "  이미지 업로드 확인", "OK");
                            if (!string.IsNullOrWhiteSpace(p.Category)) Log.Write("DEBUG", "Category", "category.success", "  카테고리 처리 확인: " + p.Category, "OK");
                        }
                        saveProducts();
                        Report(++done, total);
                        bot.HumanPause();
                    }

                    Log.Write("===== 작업 완료 =====");
                }
                catch (StopRequestedException) { CurrentProduct = null; Log.Write("사용자가 작업을 중지했습니다."); }
                catch (Exception ex)
                {
                    Log.Error("Automation", "run.error", "오류: " + ex.Message, ex);
                    try { if (!bot.IsAlive) bot.Close(); } catch { }
                }
                finally
                {
                    try { saveProducts(); } catch { }
                    lock (_gate) { Running = false; }
                    var h = Finished; if (h != null) h();
                }
            });
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        private void Report(int done, int total)
        {
            var h = Progress; if (h != null) h(done, total);
        }

        public void Stop()
        {
            if (_bot != null) _bot.StopRequested = true;
        }

        public void Shutdown()
        {
            Stop();
            try { if (_thread != null && _thread.IsAlive) _thread.Join(3000); } catch { }
            if (_bot != null) { _bot.Close(); _bot = null; }
        }
    }
}
