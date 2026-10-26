using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BunjangAuto
{
    public class MainForm : Form
    {
        private AppSettings _cfg;
        private SelectorSet _sel;
        private BindingList<Product> _products;
        private readonly Runner _runner = new Runner();

        // 계정/설정 탭

        // 상품 탭
        private DataGridView grid;

        // 실행 탭
        private CheckBox chkRegister, chkRepeat;
        private NumericUpDown numRounds;
        private CheckBox chkOneByOne, chkDup, chkWindows;
        private NumericUpDown numGap;
        private DataGridView gridRules;
        private Button bRuleAdd, bRuleDel;
        private BindingList<TimeRule> _rules = new BindingList<TimeRule>();
        private List<TimeWindow> _wins;          // 시간대별 등록 중일 때만 값이 있음
        private int _round = 1, _roundTotal = 1; // 반복 회차
        private DateTime _runStart = DateTime.MinValue;
        private readonly HashSet<Product> _skip = new HashSet<Product>();
        private Button btnStart, btnStop;
        private Label lblNext, lblState;
        private ProgressBar bar;
        private System.Windows.Forms.Timer timer;
        private DateTime _nextRun = DateTime.MinValue;
        private bool _scheduleOn;

        private RichTextBox txtLog;
        private System.Windows.Forms.Timer _guardTimer;

        public MainForm()
        {
            Text = "BunjangAuto  |  © 음악천재";
            _guardTimer = new System.Windows.Forms.Timer { Interval = 120000 };
            _guardTimer.Tick += delegate { Guard.Check(); };
            _guardTimer.Start();
            Width = 1100; Height = 870;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.FontBase();

            _cfg = Storage.LoadSettings();
            _sel = SelectorSet.Load();
            _products = Storage.LoadProducts();

            BuildUi();
            LoadSettingsToUi();
            grid.DataSource = _products;
            Theme.MarkPrimary(btnStart);
            Theme.Apply(this);

            Log.Line += OnLogLine;
            _runner.Finished += OnRunnerFinished;
            _runner.Progress += OnRunnerProgress;
            _runner.Ask = AskUser;
            _runner.ManualLoginNeeded += delegate (string m)
            {
                BeginInvoke((Action)(() => MessageBox.Show(this, m, "로그인 필요", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            };

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTimer;
            timer.Start();

            FormClosing += OnFormClosing;
            Shown += delegate { Log.Write("프로그램을 시작했습니다. 데이터 폴더: " + Paths.Root); };
        }

        // ============================ UI 구성 ============================
        private void BuildUi()
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 625 };
            Controls.Add(split);
            Controls.Add(Theme.BuildHeader("BunjangAuto", "프리미엄 번개장터 자동등록 솔루션   ·   © 음악천재"));

            var tabs = new TabControl { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(tabs);

            var tabAcc = new TabPage("계정 / 설정");
            var tabPro = new TabPage("상품 목록");
            var tabRun = new TabPage("실행 / 예약");
            tabs.TabPages.AddRange(new TabPage[] { tabAcc, tabPro, tabRun });

            BuildAccountTab(tabAcc);
            BuildProductTab(tabPro);
            BuildRunTab(tabRun);

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Theme.BgDark,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontMono(9f),
                BorderStyle = BorderStyle.None
            };
            var logHeader = new Label { Dock = DockStyle.Top, Text = "  실행 로그", Height = 24, Font = Theme.FontBold(), ForeColor = Theme.Gold, BackColor = Theme.Panel, TextAlign = ContentAlignment.MiddleLeft };
            split.Panel2.Controls.Add(txtLog);
            split.Panel2.Controls.Add(logHeader);
        }

        private void BuildAccountTab(TabPage t)
        {
            t.Padding = new Padding(16);
            var fl = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            t.Controls.Add(fl);

            fl.Controls.Add(new Label { Text = "번개장터 로그인", AutoSize = true, Font = Theme.FontBold(), ForeColor = Theme.Gold });

            var btns = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 10, 0, 0) };
            var bLogin = new Button { Text = "로그인 테스트", AutoSize = true };
            var bReset2 = new Button { Text = "로그인 상태 초기화", AutoSize = true };
            btns.Controls.AddRange(new Control[] { bLogin, bReset2 });
            fl.Controls.Add(btns);

            fl.Controls.Add(new Label
            {
                AutoSize = true,
                Padding = new Padding(0, 14, 0, 0),
                ForeColor = Theme.TextMuted,
                Text = "※ 아이디/비밀번호는 입력하지도, 저장하지도 않습니다.\r\n" +
                       "   [로그인 테스트] 또는 [시작]을 누르면 크롬 창이 열리고 번개장터 로그인 화면으로 이동합니다.\r\n" +
                       "   그 창에서 직접 로그인(문자·소셜 로그인 포함)한 뒤, 안내창의 [예]를 누르세요.\r\n" +
                       "   로그인은 전용 크롬 프로필에 유지되어 다음부터는 자동으로 이어집니다.\r\n" +
                       "   로그인이 풀린 것 같으면 [로그인 상태 초기화]를 누른 뒤 다시 로그인하세요."
            });

            bLogin.Click += delegate { RunBackground("로그인 테스트", delegate (Bot b) { b.EnsureLogin(); }); };
            bReset2.Click += delegate
            {
                _cfg.AssumeLoggedIn = false; Storage.SaveSettings(_cfg);
                Log.Write("로그인 상태를 초기화했습니다. 다음 실행 때 로그인을 다시 확인합니다.");
            };
        }

        private void BuildProductTab(TabPage t)
        {
            var bar2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
            var bImport = new Button { Text = "CSV 불러오기", AutoSize = true };
            var bExport = new Button { Text = "CSV 저장", AutoSize = true };
            var bTpl = new Button { Text = "CSV 양식 만들기", AutoSize = true };
            var bAdd = new Button { Text = "상품 추가", AutoSize = true };
            var bEdit = new Button { Text = "선택 상품 편집", AutoSize = true };
            var bDel = new Button { Text = "선택 행 삭제", AutoSize = true };
            var bImg = new Button { Text = "선택 행에 사진 지정", AutoSize = true };
            var bReset = new Button { Text = "선택 행 상태 초기화", AutoSize = true };
            bar2.Controls.AddRange(new Control[] { bAdd, bEdit, bImport, bExport, bTpl, bDel, bImg, bReset });

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window,
                RowHeadersWidth = 28,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            };
            AddCol("Title", "제목", 220);
            AddCol("Price", "가격", 80);
            AddCol("Category", "카테고리 (대>중>소)", 160);
            AddCol("Condition", "상태", 60);
            AddCol("Quantity", "수량", 45);
            AddCol("Tags", "태그(쉼표)", 120);
            AddCol("Images", "사진 경로(;)", 180);
            AddCol("Description", "설명", 220);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "FreeShipping", HeaderText = "배송비포함", Width = 70 });
            AddCol("Status", "상태메시지", 160, true);
            AddCol("ProductId", "상품번호", 90, true);
            AddCol("LastPosted", "마지막 등록", 110, true);

            t.Controls.Add(grid);
            t.Controls.Add(bar2);

            bImport.Click += delegate { ImportCsv(); };
            bExport.Click += delegate { ExportCsv(); };
            bTpl.Click += delegate
            {
                using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "상품양식.csv" })
                    if (d.ShowDialog(this) == DialogResult.OK) { CsvIO.WriteTemplate(d.FileName); Log.Write("양식 저장: " + d.FileName); }
            };
            bAdd.Click += delegate
            {
                var np = new Product();
                using (var f = new EditForm(np))
                    if (f.ShowDialog(this) == DialogResult.OK) { _products.Add(np); Storage.SaveProducts(_products); }
            };
            Action editSel = delegate
            {
                if (grid.CurrentRow == null || grid.CurrentRow.Index < 0 || grid.CurrentRow.Index >= _products.Count) return;
                using (var f = new EditForm(_products[grid.CurrentRow.Index]))
                    if (f.ShowDialog(this) == DialogResult.OK) { grid.Refresh(); Storage.SaveProducts(_products); }
            };
            bEdit.Click += delegate { editSel(); };
            grid.RowHeaderMouseDoubleClick += delegate { editSel(); };
            bDel.Click += delegate
            {
                foreach (var r in grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(x => x.Index))
                    if (r.Index >= 0 && r.Index < _products.Count) _products.RemoveAt(r.Index);
                Storage.SaveProducts(_products);
            };
            bImg.Click += delegate
            {
                if (grid.CurrentRow == null) return;
                using (var d = new OpenFileDialog { Multiselect = true, Filter = "이미지|*.jpg;*.jpeg;*.png;*.webp;*.gif|모든 파일|*.*" })
                    if (d.ShowDialog(this) == DialogResult.OK)
                        _products[grid.CurrentRow.Index].Images = string.Join(";", d.FileNames.Take(12).Select(x => EditForm.CopyPhoto(x)).ToArray());
            };
            bReset.Click += delegate
            {
                foreach (DataGridViewRow r in grid.SelectedRows)
                {
                    var p = _products[r.Index];
                    p.Status = ""; p.ProductId = ""; p.LastPosted = "";
                }
                Storage.SaveProducts(_products);
            };
            grid.CellValueChanged += delegate { };
            grid.CellEndEdit += delegate { Storage.SaveProducts(_products); };
            grid.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
        }

        private void AddCol(string prop, string header, int width, bool readOnly = false)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly
            });
        }

        private void BuildRunTab(TabPage t)
        {
            t.Padding = new Padding(16);
            var fl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            t.Controls.Add(fl);

            fl.Controls.Add(new Label { Text = "실행할 작업", AutoSize = true, Font = Theme.FontBold(), ForeColor = Theme.Gold });
            chkRegister = new CheckBox { Text = "등록 — 상품 목록의 상품을 올립니다", AutoSize = true };
            fl.Controls.Add(chkRegister);

            // ----- 1개씩 등록 + 간격 -----
            chkOneByOne = new CheckBox { Text = "1개씩 등록 — 간격을 두고 한 개씩 올리고, 모두 올리면 자동 종료", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            fl.Controls.Add(chkOneByOne);

            var one = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(22, 0, 0, 0) };
            numGap = new NumericUpDown { Minimum = 1, Maximum = 1440, Width = 70 };
            one.Controls.Add(new Label { Text = "기본 간격", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            one.Controls.Add(numGap);
            one.Controls.Add(new Label { Text = "분마다 1개씩", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            fl.Controls.Add(one);

            chkWindows = new CheckBox { Text = "시간대별로 간격 지정 — 아래 표대로 등록 (기본 간격 대신 사용)", AutoSize = true, Padding = new Padding(22, 4, 0, 0) };
            fl.Controls.Add(chkWindows);

            var winRow = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(22, 0, 0, 0) };
            gridRules = new DataGridView
            {
                Width = 400,
                Height = 110,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window
            };
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Start", HeaderText = "시작 (시:분)" });
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "End", HeaderText = "종료 (시:분)" });
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Minutes", HeaderText = "간격 (분)" });
            gridRules.DataSource = _rules;
            gridRules.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            gridRules.CellEndEdit += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _rules.Count) return;
                var r = _rules[e.RowIndex];
                int m;
                if (e.ColumnIndex == 0 && Schedule.TryParseTime(r.Start, out m) && m < 1440) r.Start = Schedule.Format(m);
                if (e.ColumnIndex == 1 && Schedule.TryParseTime(r.End, out m)) r.End = Schedule.Format(m);
                gridRules.Refresh();
            };
            winRow.Controls.Add(gridRules);

            var winBtns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            bRuleAdd = new Button { Text = "시간대 추가", AutoSize = true };
            bRuleDel = new Button { Text = "선택 삭제", AutoSize = true };
            winBtns.Controls.AddRange(new Control[] { bRuleAdd, bRuleDel });
            winRow.Controls.Add(winBtns);
            fl.Controls.Add(winRow);

            fl.Controls.Add(new Label
            {
                Text = "예) 00:00~09:00 은 40분, 09:00~18:00 은 60분 → 시간대마다 다른 간격으로 1개씩 올립니다. 시간대 밖에서는\r\n등록하지 않고 다음 시간대가 시작될 때까지 기다립니다. (자정을 넘기려면 22:00~06:00 처럼, 하루 끝은 24:00)",
                AutoSize = true,
                ForeColor = Theme.TextMuted,
                Padding = new Padding(22, 0, 0, 0)
            });

            bRuleAdd.Click += delegate
            {
                // 마지막 시간대가 끝나는 시각부터 이어서 시작하도록 기본값을 채운다
                var nr = new TimeRule("00:00", "09:00", 40);
                int endMin;
                if (_rules.Count > 0 && Schedule.TryParseTime(_rules[_rules.Count - 1].End, out endMin) && endMin < 1440)
                {
                    nr.Start = Schedule.Format(endMin);
                    nr.End = Schedule.Format(Math.Min(1440, endMin + 540));
                    nr.Minutes = Math.Max(1, _rules[_rules.Count - 1].Minutes);
                }
                _rules.Add(nr);
            };
            bRuleDel.Click += delegate
            {
                foreach (var r in gridRules.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(x => x.Index))
                    if (r.Index >= 0 && r.Index < _rules.Count) _rules.RemoveAt(r.Index);
            };

            chkDup = new CheckBox { Text = "중복 등록 허용 — 이미 등록된 상품도 다시 등록합니다", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            fl.Controls.Add(chkDup);

            // ----- 반복: 목록을 다 올리면 처음부터 다시 -----
            var rep = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 2, 0, 0) };
            chkRepeat = new CheckBox { Text = "목록을 다 올리면 처음 상품부터 다시 올리기 — 총", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            numRounds = new NumericUpDown { Minimum = 1, Maximum = 999, Width = 70 };
            rep.Controls.Add(chkRepeat);
            rep.Controls.Add(numRounds);
            rep.Controls.Add(new Label { Text = "회 (목록 전체를 한 번 올리면 1회)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            fl.Controls.Add(rep);

            var btns = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            btnStart = new Button { Text = "▶  시작", Width = 120, Height = 38 };
            btnStop = new Button { Text = "■  중지", Width = 120, Height = 38, Enabled = false };
            btns.Controls.AddRange(new Control[] { btnStart, btnStop });
            fl.Controls.Add(btns);

            lblState = new Label { Text = "대기 중", AutoSize = true, Padding = new Padding(0, 6, 0, 0), ForeColor = Theme.Gold, Font = Theme.FontBold() };
            lblNext = new Label { Text = "", AutoSize = true };
            bar = new ProgressBar { Width = 420, Height = 18, Style = ProgressBarStyle.Continuous };
            fl.Controls.AddRange(new Control[] { lblState, bar, lblNext });

            chkOneByOne.CheckedChanged += delegate { UpdateRunEnabled(); };
            chkWindows.CheckedChanged += delegate { UpdateRunEnabled(); };
            chkRepeat.CheckedChanged += delegate { UpdateRunEnabled(); };
            btnStart.Click += delegate { StartClicked(); };
            btnStop.Click += delegate { StopClicked(); };
        }

        /// <summary>선택한 옵션에 맞지 않는 칸은 눌리지 않게 막는다.</summary>
        private void UpdateRunEnabled()
        {
            bool one = chkOneByOne.Checked;
            bool win = one && chkWindows.Checked;
            numGap.Enabled = one && !chkWindows.Checked;
            chkWindows.Enabled = one;
            gridRules.Enabled = win;
            bRuleAdd.Enabled = win;
            bRuleDel.Enabled = win;
            chkRepeat.Enabled = one;
            numRounds.Enabled = one && chkRepeat.Checked;
        }

        // ============================ 설정 ↔ 화면 ============================
        private void LoadSettingsToUi()
        {
            chkRegister.Checked = _cfg.DoRegister;
            chkRepeat.Checked = _cfg.RepeatAll;
            numRounds.Value = Clamp(_cfg.RepeatCount, numRounds);
            chkOneByOne.Checked = _cfg.OneByOne;
            chkDup.Checked = _cfg.AllowDuplicate;
            numGap.Value = Clamp(_cfg.OneByOneMinutes, numGap);
            chkWindows.Checked = _cfg.UseTimeRules;
            _rules.Clear();
            if (_cfg.TimeRules != null)
                foreach (var r in _cfg.TimeRules) _rules.Add(new TimeRule(r.Start, r.End, r.Minutes));
            if (_rules.Count == 0)
            {
                // 처음 쓰는 경우의 예시 시간대
                _rules.Add(new TimeRule("00:00", "09:00", 40));
                _rules.Add(new TimeRule("09:00", "18:00", 60));
            }
            UpdateRunEnabled();
        }

        private static decimal Clamp(int v, NumericUpDown n)
        {
            return Math.Max(n.Minimum, Math.Min(n.Maximum, v));
        }

        private void ReadUiToSettings()
        {
            _cfg.DoRegister = chkRegister.Checked;
            _cfg.RepeatAll = chkRepeat.Checked;
            _cfg.RepeatCount = (int)numRounds.Value;
            _cfg.OneByOne = chkOneByOne.Checked;
            _cfg.AllowDuplicate = chkDup.Checked;
            _cfg.OneByOneMinutes = (int)numGap.Value;
            _cfg.UseTimeRules = chkWindows.Checked;
            _cfg.TimeRules = _rules.Select(r => new TimeRule(r.Start, r.End, r.Minutes)).ToList();
            Storage.SaveSettings(_cfg);
        }

        // ============================ CSV ============================
        private void ImportCsv()
        {
            using (var d = new OpenFileDialog { Filter = "CSV·텍스트 파일|*.csv;*.txt;*.tsv|모든 파일|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var items = CsvIO.Read(d.FileName);
                    bool replace = _products.Count == 0 ||
                        MessageBox.Show(this, "기존 목록을 지우고 새로 불러올까요?\r\n(아니오: 아래에 이어서 추가)", "CSV 불러오기",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                    if (replace) _products.Clear();
                    foreach (var p in items) _products.Add(p);
                    _cfg.LastCsv = d.FileName;
                    Storage.SaveProducts(_products);
                    Log.Write("CSV " + items.Count + "건을 불러왔습니다: " + d.FileName);
                    int bad = items.Count(x => (x.Description ?? "").Contains("??") || (x.Title ?? "").Contains("??"));
                    if (bad > 0)
                        Log.Write("[주의] 제목/설명에 '??' 가 있는 상품이 " + bad + "건 있습니다. 엑셀에서 'CSV(쉼표로 분리)'로 저장하면 이모지·특수기호가 '?'로 바뀌어 복구되지 않습니다. " +
                                  "원본 엑셀에서 'CSV UTF-8(쉼표로 분리)'로 다시 저장해 불러오세요.");
                }
                catch (Exception ex) { MessageBox.Show(this, "불러오기 실패: " + ex.Message, "CSV 불러오기", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void ExportCsv()
        {
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "상품목록.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { CsvIO.Write(d.FileName, _products); Log.Write("CSV 저장: " + d.FileName); }
                catch (Exception ex) { MessageBox.Show(this, "저장 실패: " + ex.Message + (ex is IOException ? "\r\n\r\n엑셀 등에서 같은 파일을 열어 두었다면 닫고 다시 저장해 주세요." : ""), "오류"); }
            }
        }

        // ============================ 실행 / 예약 ============================
        private void StartClicked()
        {
            ReadUiToSettings();
            if (!_cfg.DoRegister)
            {
                MessageBox.Show(this, "실행할 작업을 선택해 주세요.", "안내");
                return;
            }
            _skip.Clear();
            _round = 1;
            _roundTotal = (_cfg.OneByOne && _cfg.RepeatAll) ? Math.Max(1, _cfg.RepeatCount) : 1;
            _wins = null;
            if (_cfg.OneByOne && _cfg.UseTimeRules)
            {
                string err;
                List<TimeWindow> wins;
                if (!Schedule.TryBuild(_cfg.TimeRules, out wins, out err))
                {
                    MessageBox.Show(this, err, "시간대 설정 확인");
                    return;
                }
                _wins = wins;
                Log.Write("[시간대별 등록] " + Schedule.Describe(_wins));
            }
            if (_roundTotal > 1) Log.Write("[반복] 목록을 총 " + _roundTotal + "회 올립니다. (1/" + _roundTotal + "회차 시작)");
            _scheduleOn = _cfg.OneByOne;
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            if (_wins != null && Schedule.ActiveAt(_wins, DateTime.Now) == null)
            {
                // 지금은 등록 시간대가 아니므로 다음 시간대가 시작될 때까지 기다린다
                _nextRun = Schedule.NextAllowed(_wins, DateTime.Now);
                lblState.Text = "시간대 시작 대기 중";
                Log.Write("[시간대별 등록] 지금은 등록 시간대가 아니어서 " + _nextRun.ToString("MM-dd HH:mm") + " 에 시작합니다.");
                return;
            }
            LaunchRun();
        }

        private void LaunchRun()
        {
            lblState.Text = "실행 중...";
            lblNext.Text = "";
            bar.Value = 0;
            _runStart = DateTime.Now;
            bool includeRegistered = _cfg.AllowDuplicate || _round > 1;   // 2회차부터는 이미 등록된 상품도 처음부터 다시
            _runner.Run(_cfg, _sel, _products, () => _products.Where(p => !_skip.Contains(p)).ToList(), () => Storage.SaveProducts(_products), includeRegistered);
        }

        private void StopClicked()
        {
            _scheduleOn = false;
            _nextRun = DateTime.MinValue;
            _runner.Stop();
            lblState.Text = "중지하는 중...";
            if (!_runner.Running) FinishUi();
        }

        /// <summary>이번 회차에 아직 등록할 상품이 남았는지</summary>
        private bool HasPending()
        {
            bool includeRegistered = _cfg.AllowDuplicate || _round > 1;
            return _products.Any(p => p.IsValid && !_skip.Contains(p) &&
                (includeRegistered || (string.IsNullOrEmpty(p.ProductId) && p.Status != "등록됨")));
        }

        private string RoundText()
        {
            return _roundTotal > 1 ? "  ·  " + _round + "/" + _roundTotal + "회차" : "";
        }

        private void OnRunnerFinished()
        {
            if (IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                grid.Refresh();
                if (!_scheduleOn) { FinishUi(); return; }

                // 이번 회차에 이미 시도한 상품(성공/실패)은 같은 회차 안에서 다시 시도하지 않는다
                foreach (var p in _runner.LastRegistered) _skip.Add(p);
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
                        return;
                    }
                }

                if (_wins != null) _nextRun = Schedule.NextRun(_wins, _runStart, DateTime.Now, _cfg.OneByOneMinutes);
                else
                {
                    _nextRun = _runStart.AddMinutes(_cfg.OneByOneMinutes);
                    if (_nextRun < DateTime.Now) _nextRun = DateTime.Now;
                }
                Log.Write("[1개씩 등록] 다음 등록 예정: " + _nextRun.ToString(_nextRun.Date == DateTime.Today ? "HH:mm:ss" : "MM-dd HH:mm:ss"));
                lblState.Text = "다음 실행 대기 중" + RoundText();
            }));
        }

        private void FinishUi()
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblState.Text = "대기 중";
            lblNext.Text = "";
        }

        private void OnRunnerProgress(int done, int total)
        {
            if (IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                bar.Maximum = Math.Max(1, total);
                bar.Value = Math.Min(done, bar.Maximum);
                lblState.Text = "실행 중... (" + done + "/" + total + ")" + RoundText();
                grid.Refresh();
            }));
        }

        private void OnTimer(object s, EventArgs e)
        {
            if (!_scheduleOn || _runner.Running || _nextRun == DateTime.MinValue) return;
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
                lblNext.Text = "다음 실행까지 " + t + "  (" + _nextRun.ToString("MM-dd HH:mm") + ")";
            }
        }

        // ============================ 로그 / 보조 ============================
        private void OnLogLine(string line)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    txtLog.AppendText(line + Environment.NewLine);
                    txtLog.SelectionStart = txtLog.TextLength;
                    txtLog.ScrollToCaret();
                }));
            }
            catch { }
        }

        /// <summary>로그인 테스트·점검처럼 짧은 단발 작업을 브라우저로 실행</summary>
        private void RunBackground(string name, Action<Bot> work)
        {
            if (_runner.Running) { MessageBox.Show(this, "다른 작업이 실행 중입니다.", "안내"); return; }
            ReadUiToSettings();
            var bot = new Bot(_cfg, _sel);
            bot.NotifyManual = m => BeginInvoke((Action)(() => MessageBox.Show(this, m, "로그인 필요")));
            bot.Ask = AskUser;
            var th = new System.Threading.Thread(() =>
            {
                try { Log.Write("[" + name + "] 시작"); work(bot); Log.Write("[" + name + "] 끝"); }
                catch (Exception ex) { Log.Write("[" + name + "] 오류: " + ex.Message); }
                finally { bot.Close(); }
            });
            th.IsBackground = true;
            th.Start();
        }

        /// <summary>백그라운드 스레드에서 호출해도 안전한 예/아니오 질문</summary>
        private bool AskUser(string msg)
        {
            if (IsDisposed) return false;
            return (bool)Invoke((Func<bool>)(() =>
                MessageBox.Show(this, msg, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes));
        }

        private void OnFormClosing(object s, FormClosingEventArgs e)
        {
            if (_runner.Running &&
                MessageBox.Show(this, "작업이 진행 중입니다. 종료할까요?", "종료", MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                e.Cancel = true; return;
            }
            try { ReadUiToSettings(); } catch { }
            Storage.SaveProducts(_products);
            _runner.Shutdown();
        }
    }
}
