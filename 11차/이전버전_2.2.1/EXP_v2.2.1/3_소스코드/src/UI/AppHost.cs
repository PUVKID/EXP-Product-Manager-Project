using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>모든 화면이 함께 쓰는 상태와 기능(설정, 상품 목록, Runner, 자동등록 컨트롤러, 관리자 세션, 안내 창).</summary>
    public class AppHost
    {
        public MainForm Form;
        public AppSettings Cfg;
        public SelectorSet Sel;
        public BindingList<Product> Products;
        public Runner Runner;
        public AutomationController Auto;
        public AdminApiClient Admin;

        public event Action AdminChanged;
        public event Action LoginChanged;

        public bool IsAdmin { get { return Admin != null && Admin.IsAdmin; } }

        public void SetAdmin(AdminApiClient c)
        {
            Admin = c;
            var h = AdminChanged; if (h != null) h();
        }

        public void RaiseLoginChanged() { var h = LoginChanged; if (h != null) h(); }

        public void Navigate(string key) { Form.Navigate(key); }

        public void SaveSettings() { Auto.SaveSettings(); }

        /// <summary>백그라운드 스레드에서 불러도 안전한 예/아니오 질문</summary>
        public bool Ask(string msg)
        {
            if (Form.IsDisposed) return false;
            return (bool)Form.Invoke((Func<bool>)(() => FluentBox.Show(Form, msg, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes));
        }

        public void Say(string msg, string title) { FluentBox.Show(Form, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information); }

        /// <summary>로그인 테스트·점검처럼 짧은 단발 작업을 브라우저로 실행 (예전 MainForm.RunBackground 와 동일)</summary>
        public void RunBackground(string name, Action<Bot> work)
        {
            if (Runner.Running) { FluentBox.Show(Form, "다른 작업이 실행 중입니다.", "안내"); return; }
            SaveSettings();
            var bot = new Bot(Cfg, Sel);
            bot.NotifyManual = m => Form.BeginInvoke((Action)(() => FluentBox.Show(Form, m, "로그인 필요", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            bot.Ask = Ask;
            var th = new System.Threading.Thread(() =>
            {
                try { Log.Write("[" + name + "] 시작"); work(bot); Log.Write("[" + name + "] 끝"); }
                catch (Exception ex) { Log.Error("Automation", "task.error", "[" + name + "] 오류: " + ex.Message, ex); }
                finally { bot.Close(); try { Form.BeginInvoke((Action)RaiseLoginChanged); } catch { } }
            });
            th.IsBackground = true;
            th.Start();
        }

        // ---- Dashboard 용 집계 (Product 상태 문구는 Bot 이 정한 값 그대로 사용)
        public static bool IsDone(Product p) { return p.Status == "등록됨" || !string.IsNullOrEmpty(p.ProductId); }
        public static bool IsFail(Product p) { return (p.Status ?? "").StartsWith("실패"); }
        public int CountDone() { int n = 0; foreach (var p in Products) if (IsDone(p)) n++; return n; }
        public int CountFail() { int n = 0; foreach (var p in Products) if (!IsDone(p) && IsFail(p)) n++; return n; }
        public int CountPending() { int n = 0; foreach (var p in Products) if (p.IsValid && !IsDone(p) && !IsFail(p)) n++; return n; }
    }
}
