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
        private TextBox txtId, txtPw;
        private CheckBox chkSave, chkHeadless;
        private NumericUpDown numDelayMin, numDelayMax, numMax;

        // 상품 탭
        private DataGridView grid;

        // 실행 탭
        private CheckBox chkRegister, chkBump, chkRepost, chkRepeat;
        private NumericUpDown numInterval;
        private CheckBox chkOneByOne, chkDup;
        private NumericUpDown numGap;
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
            Text = "번개장터 자동등록  |  © 음악천재";
            _guardTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _guardTimer.Tick += delegate { Guard.Check(); };
            _guardTimer.Start();
            Width = 1100; Height = 780;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 9f);

            _cfg = Storage.LoadSettings();
            _sel = SelectorSet.Load();
            _products = Storage.LoadProducts();

            BuildUi();
            LoadSettingsToUi();
            grid.DataSource = _products;

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
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 480 };
            Controls.Add(split);

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
                BackColor = Color.FromArgb(24, 26, 31),
                ForeColor = Color.FromArgb(220, 224, 230),
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None
            };
            split.Panel2.Controls.Add(txtLog);
        }

        private void BuildAccountTab(TabPage t)
        {
            t.Padding = new Padding(16);
            var tl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.Controls.Add(tl);

            txtId = new TextBox { Width = 280 };
            txtPw = new TextBox { Width = 280, UseSystemPasswordChar = true };
            chkSave = new CheckBox { Text = "아이디/비밀번호 저장 (이 PC에서만 복호화되도록 암호화)", AutoSize = true };
            chkHeadless = new CheckBox { Text = "브라우저 창 숨기기 (로그인이 풀린 상태에서는 사용하지 마세요)", AutoSize = true };
            numDelayMin = new NumericUpDown { Minimum = 0, Maximum = 600, Width = 70 };
            numDelayMax = new NumericUpDown { Minimum = 0, Maximum = 600, Width = 70 };
            numMax = new NumericUpDown { Minimum = 1, Maximum = 500, Width = 70 };

            AddRow(tl, "아이디", txtId);
            AddRow(tl, "비밀번호", txtPw);
            AddRow(tl, "", chkSave);
            AddRow(tl, "", chkHeadless);
            AddRow(tl, "작업 사이 대기(초) 최소", numDelayMin);
            AddRow(tl, "작업 사이 대기(초) 최대", numDelayMax);
            AddRow(tl, "1회 실행당 최대 처리 수", numMax);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 14, 0, 0) };
            var bLogin = new Button { Text = "로그인 테스트", AutoSize = true };
            var bCheck = new Button { Text = "화면 요소 점검", AutoSize = true };
            var bSel = new Button { Text = "selectors.json 열기", AutoSize = true };
            var bFolder = new Button { Text = "데이터 폴더 열기", AutoSize = true };
            var bDiag = new Button { Text = "진단 파일 만들기", AutoSize = true };
            var bReset2 = new Button { Text = "로그인 상태 초기화", AutoSize = true };
            btns.Controls.AddRange(new Control[] { bLogin, bCheck, bDiag, bReset2, bSel, bFolder });
            t.Controls.Add(btns);
            btns.BringToFront();

            var tip = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 14, 0, 0),
                Text = "※ 로그인은 전용 크롬 프로필에 유지됩니다. 문자/소셜 로그인처럼 자동 입력이 안 되면\r\n   열린 크롬 창에서 한 번만 직접 로그인하면 다음부터는 자동으로 이어집니다."
            };
            t.Controls.Add(tip);
            tip.BringToFront();

            bLogin.Click += delegate { RunBackground("로그인 테스트", delegate (Bot b) { b.EnsureLogin(txtId.Text.Trim(), txtPw.Text); }); };
            bCheck.Click += delegate
            {
                RunBackground("화면 요소 점검", delegate (Bot b)
                {
                    foreach (var line in b.CheckSelectors()) Log.Write("  " + line);
                    Log.Write("※ '없음' 항목은 selectors.json 의 XPath 를 사이트 화면에 맞게 고쳐 주세요.");
                });
            };
            bDiag.Click += delegate
            {
                RunBackground("진단 파일 만들기", delegate (Bot b)
                {
                    if (!b.EnsureLogin(txtId.Text.Trim(), txtPw.Text)) return;
                    string zip = b.DumpPages();
                    Log.Write("진단 파일 저장 완료: " + zip);
                    Log.Write("※ 화면에 개인정보(닉네임 등)가 보일 수 있으니 확인 후 전달하세요.");
                    try { Process.Start("explorer.exe", "/select,\"" + zip + "\""); } catch { }
                });
            };
            bReset2.Click += delegate
            {
                _cfg.AssumeLoggedIn = false; Storage.SaveSettings(_cfg);
                Log.Write("로그인 상태를 초기화했습니다. 다음 실행 때 로그인을 다시 확인합니다.");
            };
            bSel.Click += delegate { _sel.Save(); Process.Start("notepad.exe", Paths.Selectors); };
            bFolder.Click += delegate { Process.Start("explorer.exe", Paths.Root); };
        }

        private static void AddRow(TableLayoutPanel tl, string label, Control c)
        {
            int r = tl.RowCount++;
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 6) }, 0, r);
            c.Margin = new Padding(3, 4, 3, 4);
            tl.Controls.Add(c, 1, r);
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
            var fl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            t.Controls.Add(fl);

            fl.Controls.Add(new Label { Text = "실행할 작업", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
            chkRegister = new CheckBox { Text = "신규 등록 — 아직 등록되지 않은 상품을 올립니다", AutoSize = true };
            chkBump = new CheckBox { Text = "끌올 — 내 상점의 상품을 끌어올립니다", AutoSize = true };
            chkRepost = new CheckBox { Text = "삭제 후 재등록 — 등록된 상품을 지우고 새로 올립니다", AutoSize = true };
            fl.Controls.AddRange(new Control[] { chkRegister, chkBump, chkRepost });

            var one = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
            chkOneByOne = new CheckBox { Text = "신규 등록을 1개씩", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            numGap = new NumericUpDown { Minimum = 1, Maximum = 1440, Width = 70 };
            one.Controls.Add(chkOneByOne);
            one.Controls.Add(numGap);
            one.Controls.Add(new Label { Text = "분 간격으로 (모두 등록되면 자동 종료)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            fl.Controls.Add(one);
            chkDup = new CheckBox { Text = "중복 등록 허용 — 이미 등록된 상품도 다시 등록합니다", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            fl.Controls.Add(chkDup);

            var rep = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
            chkRepeat = new CheckBox { Text = "반복 실행", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            numInterval = new NumericUpDown { Minimum = 1, Maximum = 10080, Width = 70 };
            rep.Controls.Add(chkRepeat);
            rep.Controls.Add(numInterval);
            rep.Controls.Add(new Label { Text = "분마다", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            fl.Controls.Add(rep);

            var btns = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 14, 0, 0) };
            btnStart = new Button { Text = "▶ 시작", Width = 110, Height = 36 };
            btnStop = new Button { Text = "■ 중지", Width = 110, Height = 36, Enabled = false };
            btns.Controls.AddRange(new Control[] { btnStart, btnStop });
            fl.Controls.Add(btns);

            lblState = new Label { Text = "대기 중", AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
            lblNext = new Label { Text = "", AutoSize = true };
            bar = new ProgressBar { Width = 420, Height = 18 };
            fl.Controls.AddRange(new Control[] { lblState, bar, lblNext });

            btnStart.Click += delegate { StartClicked(); };
            btnStop.Click += delegate { StopClicked(); };
        }

        // ============================ 설정 ↔ 화면 ============================
        private void LoadSettingsToUi()
        {
            txtId.Text = _cfg.Id;
            txtPw.Text = Storage.Unprotect(_cfg.PwProtected);
            chkSave.Checked = _cfg.SaveAccount;
            chkHeadless.Checked = _cfg.Headless;
            numDelayMin.Value = Clamp(_cfg.DelayMinSec, numDelayMin);
            numDelayMax.Value = Clamp(_cfg.DelayMaxSec, numDelayMax);
            numMax.Value = Clamp(_cfg.MaxPerRun, numMax);
            chkRegister.Checked = _cfg.DoRegister;
            chkBump.Checked = _cfg.DoBump;
            chkRepost.Checked = _cfg.DoRepost;
            chkRepeat.Checked = _cfg.Repeat;
            numInterval.Value = Clamp(_cfg.IntervalMinutes, numInterval);
            chkOneByOne.Checked = _cfg.OneByOne;
            chkDup.Checked = _cfg.AllowDuplicate;
            numGap.Value = Clamp(_cfg.OneByOneMinutes, numGap);
        }

        private static decimal Clamp(int v, NumericUpDown n)
        {
            return Math.Max(n.Minimum, Math.Min(n.Maximum, v));
        }

        private void ReadUiToSettings()
        {
            _cfg.SaveAccount = chkSave.Checked;
            _cfg.Id = chkSave.Checked ? txtId.Text.Trim() : "";
            _cfg.PwProtected = chkSave.Checked ? Storage.Protect(txtPw.Text) : "";
            _cfg.Headless = chkHeadless.Checked;
            _cfg.DelayMinSec = (int)numDelayMin.Value;
            _cfg.DelayMaxSec = (int)numDelayMax.Value;
            _cfg.MaxPerRun = (int)numMax.Value;
            _cfg.DoRegister = chkRegister.Checked;
            _cfg.DoBump = chkBump.Checked;
            _cfg.DoRepost = chkRepost.Checked;
            _cfg.Repeat = chkRepeat.Checked;
            _cfg.IntervalMinutes = (int)numInterval.Value;
            _cfg.OneByOne = chkOneByOne.Checked;
            _cfg.AllowDuplicate = chkDup.Checked;
            _cfg.OneByOneMinutes = (int)numGap.Value;
            Storage.SaveSettings(_cfg);
        }

        // ============================ CSV ============================
        private void ImportCsv()
        {
            using (var d = new OpenFileDialog { Filter = "CSV|*.csv|모든 파일|*.*" })
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
                catch (Exception ex) { MessageBox.Show(this, "불러오기 실패: " + ex.Message, "오류"); }
            }
        }

        private void ExportCsv()
        {
            using (var d = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "상품목록.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { CsvIO.Write(d.FileName, _products); Log.Write("CSV 저장: " + d.FileName); }
                catch (Exception ex) { MessageBox.Show(this, "저장 실패: " + ex.Message, "오류"); }
            }
        }

        // ============================ 실행 / 예약 ============================
        private void StartClicked()
        {
            ReadUiToSettings();
            if (!_cfg.DoRegister && !_cfg.DoBump && !_cfg.DoRepost)
            {
                MessageBox.Show(this, "실행할 작업을 하나 이상 선택해 주세요.", "안내");
                return;
            }
            _skip.Clear();
            _scheduleOn = _cfg.Repeat || (_cfg.OneByOne && _cfg.DoRegister);
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            LaunchRun();
        }

        private void LaunchRun()
        {
            lblState.Text = "실행 중...";
            lblNext.Text = "";
            bar.Value = 0;
            _runStart = DateTime.Now;
            _runner.Run(_cfg, _sel, _products, () => _products.Where(p => !_skip.Contains(p)).ToList(), () => Storage.SaveProducts(_products));
        }

        private void StopClicked()
        {
            _scheduleOn = false;
            _nextRun = DateTime.MinValue;
            _runner.Stop();
            lblState.Text = "중지하는 중...";
            if (!_runner.Running) FinishUi();
        }

        private void OnRunnerFinished()
        {
            if (IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                grid.Refresh();
                if (_scheduleOn)
                {
                    if (_cfg.OneByOne && _cfg.DoRegister)
                    {
                        // 이번에 실패한 상품은 이번 예약 동안 건너뛰어 다음 상품이 막히지 않게 한다
                        foreach (var p in _products)
                            if ((p.Status ?? "").StartsWith("실패")) _skip.Add(p);
                        if (_cfg.AllowDuplicate) foreach (var p in _runner.LastRegistered) _skip.Add(p);
                        bool pending = _cfg.AllowDuplicate
                            ? _products.Any(p => p.IsValid && !_skip.Contains(p))
                            : _products.Any(p => p.IsValid && string.IsNullOrEmpty(p.ProductId) && p.Status != "등록됨" && !_skip.Contains(p));
                        if (!pending && !_cfg.DoBump && !_cfg.DoRepost)
                        {
                            _scheduleOn = false;
                            _nextRun = DateTime.MinValue;
                            Log.Write("[1개씩 등록] 남은 등록 상품이 없어 종료합니다." + (_skip.Count > 0 ? " (실패해서 건너뛴 상품 " + _skip.Count + "건)" : ""));
                            FinishUi();
                            return;
                        }
                        _nextRun = _runStart.AddMinutes(_cfg.OneByOneMinutes);
                        if (_nextRun < DateTime.Now) _nextRun = DateTime.Now;
                        Log.Write("[1개씩 등록] 다음 등록 예정: " + _nextRun.ToString("HH:mm:ss"));
                    }
                    else _nextRun = DateTime.Now.AddMinutes(_cfg.IntervalMinutes);
                    lblState.Text = "다음 실행 대기 중";
                }
                else FinishUi();
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
                lblState.Text = "실행 중... (" + done + "/" + total + ")";
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
            else lblNext.Text = "다음 실행까지 " + ((int)left.TotalMinutes) + "분 " + left.Seconds + "초";
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
