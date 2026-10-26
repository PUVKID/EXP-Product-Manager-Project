using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>
    /// v2 메인 창: 왼쪽 사이드바 + 상단 헤더 + 오른쪽 페이지. 상태/로직은 AppHost·AutomationController 가 갖고 있고,
    /// 이 창은 화면 구성, 페이지 전환, 테마 적용, 종료 처리(기존과 동일)만 맡는다.
    /// </summary>
    public class MainForm : Form
    {
        private readonly AppHost host = new AppHost();
        private readonly Panel content;
        private readonly SidebarPanel sidebar;
        private readonly HeaderPanel header;
        private readonly Label lblBrand, lblBrandSub, lblPage, lblFoot;
        private readonly Chip chipLic;
        private readonly Segmented segTheme;
        private readonly Dictionary<string, NavItem> navs = new Dictionary<string, NavItem>();
        private readonly Dictionary<string, PageBase> pages = new Dictionary<string, PageBase>();
        private readonly Dictionary<string, Func<PageBase>> factories = new Dictionary<string, Func<PageBase>>();
        private PageBase _current;
        private readonly Timer timer, _guardTimer;
        private int _tick;
        private bool _compact;

        private static readonly string[][] MenuDef = new[]
        {
            new[] { "dashboard", "한눈에 보기", "\uE80F" },
            new[] { "products", "상품 관리", "\uE719" },
            new[] { "automation", "자동 등록", "\uE768" },
            new[] { "login", "로그인", "\uE77B" },
            new[] { "schedule", "예약", "\uE787" },
            new[] { "help", "사용 설명서", "\uE897" },
            new[] { "settings", "설정", "\uE713" },
            new[] { "license", "라이선스", "\uE72E" },
        };

        public MainForm()
        {
            Text = Brand.Name;
            AutoScaleMode = AutoScaleMode.None;   // 배율은 Ui.ScaleTree 가 직접 처리(나중에 만든 화면에도 똑같이 적용하기 위해)
            Width = Ui.S(1320); Height = Ui.S(1000); MinimumSize = new Size(Ui.S(980), Ui.S(640));
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.FontBase();
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _guardTimer = new Timer { Interval = 120000 };
            _guardTimer.Tick += delegate { Guard.Check(); };
            _guardTimer.Start();

            // ---- 상태 불러오기 (예전과 동일: 설정 → 선택자 → 상품 목록)
            ServerClock.RefreshAsync();
            host.Form = this;
            host.Cfg = Storage.LoadSettings();
            host.Sel = SelectorSet.Load();
            host.Products = Storage.LoadProducts();
            host.Runner = new Runner();
            host.Auto = new AutomationController(host.Cfg, host.Sel, host.Products, host.Runner, this);
            host.Auto.Notice = (m, t) => FluentBox.Show(this, m, t);
            ThemeService.Init(host.Cfg);

            host.Runner.Ask = host.Ask;
            host.Runner.ManualLoginNeeded += delegate (string m)
            {
                BeginInvoke((Action)(() => FluentBox.Show(this, m, "로그인 필요", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            };

            // ---- 화면 뼈대
            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgDark };
            header = new HeaderPanel { Dock = DockStyle.Top, Height = Ui.S(70) };
            sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = Ui.S(216) };
            Controls.Add(content); Controls.Add(header); Controls.Add(sidebar);

            lblBrand = new Label { Text = Brand.Name, AutoSize = true, Font = Theme.Font(21f, FontStyle.Bold), Location = new Point(82, 28), Tag = "t", BackColor = Theme.Sidebar };
            lblBrandSub = new Label { Text = Brand.Tagline, AutoSize = true, Font = Theme.FontSub(), Location = new Point(24, 92), Tag = "m", BackColor = Theme.Sidebar };
            sidebar.Controls.Add(new BrandMark { Left = 20, Top = 20, Width = 58, Height = 58 });
            sidebar.Controls.Add(lblBrand); sidebar.Controls.Add(lblBrandSub);
            lblFoot = new Label { Text = "EXP  /  " + Brand.Version, AutoSize = true, Font = Theme.Font(8.5f, FontStyle.Regular), Tag = "m", BackColor = Theme.Sidebar };
            sidebar.Controls.Add(lblFoot);

            int y = 142;
            foreach (var m in MenuDef)
            {
                var item = new NavItem { Key = m[0], Text = m[1], Glyph = m[2], Left = 0, Top = y, Width = sidebar.Width, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                string key = m[0];
                item.Click += delegate { Navigate(key); };
                navs[key] = item; sidebar.Controls.Add(item);
                y += 50;
            }

            lblPage = new Label { Text = "", AutoSize = true, Font = Theme.Font(12f, FontStyle.Bold), Location = new Point(24, 17), Tag = "t", BackColor = Theme.BgDark };
            segTheme = new Segmented { Width = Ui.S(216), Height = Ui.S(32) };
            segTheme.Items.AddRange(new[] { "라이트", "다크", "시스템" });
            segTheme.SetIndexSilently((int)ThemeService.Mode);
            segTheme.SelectedIndexChanged += delegate { ThemeService.SetMode((ThemeMode)segTheme.SelectedIndex); };
            chipLic = new Chip { Text = "라이선스" };
            header.Controls.AddRange(new Control[] { lblPage, chipLic, segTheme });
            header.Resize += delegate { LayoutHeader(); };

            // ---- 페이지 공장 (처음 열 때 만든다)
            factories["dashboard"] = () => new DashboardPage(host);
            factories["products"] = () => new ProductsPage(host);
            factories["automation"] = () => new AutomationPage(host);
            factories["login"] = () => new LoginPage(host);
            factories["schedule"] = () => new SchedulePage(host);
            factories["help"] = () => new HelpPage(host);
            factories["settings"] = () => new SettingsPage(host);
            factories["license"] = () => new LicensePage(host);

            Theme.Changed += OnThemeChanged;
            Resize += delegate { UpdateCompact(); };

            timer = new Timer { Interval = 1000 };
            timer.Tick += delegate { host.Auto.Tick(); if (++_tick % 20 == 0) SyncChips(); };
            timer.Start();

            // 로그 기록은 Log 가 파일·이벤트로 처리한다(LogPage 가 구독). 종료 처리는 예전과 같다.
            FormClosing += OnFormClosing;
            Shown += delegate { Log.Write("프로그램을 시작했습니다. 데이터 폴더: " + Paths.Root); };
            HandleCreated += delegate { Dwm.Apply(this, Theme.Current); };

            Ui.ScaleTree(sidebar); Ui.ScaleTree(header);   // 위치는 96dpi 기준으로 적고 한 번에 현재 배율로
            ApplyAll();
            SyncChips();
            Navigate("dashboard");
            LayoutSidebar();
        }

        // ============================ 내비게이션 ============================
        public void Navigate(string key)
        {
            if (!factories.ContainsKey(key)) return;
            PageBase page;
            if (!pages.TryGetValue(key, out page))
            {
                page = factories[key]();
                page.Visible = false;
                Ui.ScaleTree(page);
                content.Controls.Add(page);
                pages[key] = page;
                Theme.Apply(page);
                page.OnThemeChanged();
            }
            if (_current != null && _current != page) { _current.OnDeactivated(); _current.Visible = false; }
            _current = page;
            page.Visible = true; page.BringToFront();
            foreach (var kv in navs) kv.Value.Selected = kv.Key == key;
            string title = MenuDef.First(m => m[0] == key)[1];
            lblPage.Text = "EXP  /  작업 공간";
            page.OnActivated();
            LayoutHeader();
        }

        private void SyncChips()
        {
            var li = LicenseGate.LastInfo;
            if (li != null && li.Valid)
            {
                Text = Brand.Name + " " + li.Edition; lblBrandSub.Text = li.Edition + "  ·  " + Lumivolt.Security.EditionPolicy.DailyLimit(li.Edition) + "개 / 일";
                string tail = li.Expires == DateTime.MaxValue ? "무기한" : "D-" + Math.Max(0, (int)Math.Ceiling((li.Expires.Date.AddDays(1) - DateTime.Now).TotalDays));
                chipLic.Text = "라이선스 정상 · " + tail; chipLic.Tone = Theme.Success;
            }
            else { chipLic.Text = "라이선스 확인 필요"; chipLic.Tone = Theme.Danger; }
            chipLic.Invalidate(); LayoutHeader();
        }

        // ============================ 레이아웃 / 테마 ============================
        private void UpdateCompact()
        {
            bool c = ClientSize.Width < Ui.S(1060) && Theme.HasIconFont;
            if (c == _compact) return;
            _compact = c; LayoutSidebar();
        }

        private void LayoutSidebar()
        {
                        sidebar.Width = Ui.S(_compact ? 76 : 224);
            lblBrand.Visible = lblBrandSub.Visible = !_compact;
            foreach (Control c in sidebar.Controls) if (c is BrandMark) c.Visible = !_compact;
            int y = Ui.S(142); int step = Ui.S(50);
            foreach (var m in MenuDef)
            {
                var it = navs[m[0]]; it.Width = sidebar.Width; it.Left = 0;
                if (!it.Visible) continue;
                it.Top = y; y += step;
            }
            lblFoot.Visible = !_compact;
            lblFoot.Location = new Point(Ui.S(20), sidebar.Height - lblFoot.Height - Ui.S(14));
            sidebar.Resize -= OnSidebarResize; sidebar.Resize += OnSidebarResize;
        }
        private void OnSidebarResize(object s, EventArgs e) { lblFoot.Location = new Point(Ui.S(20), sidebar.Height - lblFoot.Height - Ui.S(14)); }

        private void LayoutHeader()
        {
            int pad = Ui.S(20); int x = header.ClientSize.Width - pad;
            segTheme.Location = new Point(x - segTheme.Width, (header.Height - segTheme.Height) / 2); x = segTheme.Left - Ui.S(14);
            chipLic.Location = new Point(x - chipLic.Width, (header.Height - chipLic.Height) / 2);
            lblPage.Location = new Point(Ui.S(24), (header.Height - lblPage.Height) / 2);
        }

        private void OnThemeChanged() { if (!IsDisposed) ApplyAll(); }

        private void ApplyAll()
        {
            SuspendLayout();
            BackColor = Theme.BgDark; ForeColor = Theme.TextPrimary;
            content.BackColor = Theme.BgDark;
            Theme.Apply(this);
            sidebar.ApplyTheme(); header.ApplyTheme();
            lblBrand.BackColor = lblBrandSub.BackColor = lblFoot.BackColor = Theme.Sidebar;
            lblBrand.ForeColor = Theme.TextPrimary; lblBrandSub.ForeColor = Theme.TextMuted; lblFoot.ForeColor = Theme.TextMuted;
            lblPage.BackColor = Theme.BgDark; lblPage.ForeColor = Theme.TextPrimary;
            segTheme.SetIndexSilently((int)ThemeService.Mode);
            foreach (var p in pages.Values) p.OnThemeChanged();
            SyncChips();
            Invalidate(true);
            ResumeLayout(true);
        }

        // 숨겨진 페이지와 창이 남긴 시각 갱신 타이머 및 테마 구독을 해제합니다.
        protected override void Dispose(bool disposing)
        {
            if(disposing){Theme.Changed-=OnThemeChanged;if(_current!=null)_current.OnDeactivated();if(timer!=null)timer.Dispose();if(_guardTimer!=null)_guardTimer.Dispose();}
            base.Dispose(disposing);
        }

        // ============================ 종료 (예전과 동일) ============================
        private void OnFormClosing(object s, FormClosingEventArgs e)
        {
            if (host.Runner.Running &&
                FluentBox.Show(this, "작업이 진행 중입니다. 종료할까요?", "종료", MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                e.Cancel = true; return;
            }
            try { host.Auto.SaveSettings(); } catch { }
            Storage.SaveProducts(host.Products);
            host.Runner.Shutdown();
        }
    }

    public class SidebarPanel : Panel, IThemed
    {
        public SidebarPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); ApplyTheme(); }
        public void ApplyTheme() { BackColor = Theme.Sidebar; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Sidebar);
            using (var p = new Pen(Theme.Current.Border)) e.Graphics.DrawLine(p, Width - 1, 0, Width - 1, Height);
        }
    }

    public class HeaderPanel : Panel, IThemed
    {
        public HeaderPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); ApplyTheme(); }
        public void ApplyTheme() { BackColor = Theme.BgDark; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.BgDark);

        }
    }
}
