using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>Log.Write() 로 남는 모든 기록을 시간·Level·Module·Action·Message·Result 열로 보여 준다. 예전 로그 문구는 Message 열에 그대로 나온다.</summary>
    public class LogPage : PageBase
    {
        private readonly DataGridView grid;
        private readonly TextBox txtSearch;
        private readonly ComboBox cmbLevel;
        private readonly ToggleSwitch swScroll;
        private readonly Label lblCount;
        private readonly List<LogEntry> _all = new List<LogEntry>();
        private List<LogEntry> _shown = new List<LogEntry>();
        private readonly object _gate = new object();
        private readonly List<LogEntry> _pending = new List<LogEntry>();
        private readonly Timer _flush = new Timer { Interval = 300 };
        private const int Max = 5000;

        public LogPage(AppHost host) : base(host)
        {
            Key = "logs"; PageTitle = "로그";
            AutoScroll = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = Theme.BgDark };
            var title = Ui.Title("로그"); title.Location = new Point(0, 0);
            lblCount = Ui.Muted(""); lblCount.Location = new Point(2, 38);
            txtSearch = Ui.Input(260); txtSearch.Location = new Point(0, 72);
            cmbLevel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Location = new Point(272, 71), Font = Theme.FontBase() };
            cmbLevel.Items.AddRange(new object[] { "전체 Level", "INFO 이상", "WARN 이상", "ERROR만" }); cmbLevel.SelectedIndex = 0;
            swScroll = Ui.Toggle("자동 스크롤", true); swScroll.Location = new Point(400, 70); swScroll.Width = 150;
            var bFolder = Ui.Button("로그 폴더 열기", "\uE838", BtnStyle.Standard, 160); bFolder.Location = new Point(560, 68);
            var bClear = Ui.Button("화면 지우기", "\uE74D", BtnStyle.Standard, 130); bClear.Location = new Point(730, 68);
            top.Controls.AddRange(new Control[] { title, lblCount, txtSearch, cmbLevel, swScroll, bFolder, bClear });

            grid = new DataGridView
            {
                Dock = DockStyle.Fill, VirtualMode = true, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
                AllowUserToResizeRows = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시간", Width = 96, Name = "t" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Level", Width = 70, Name = "l" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Module", Width = 120, Name = "m" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", Width = 170, Name = "a" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 240, Name = "msg" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", Width = 70, Name = "r" });
            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            card.Controls.Add(grid);
            Controls.Add(card); Controls.Add(top);
            Theme.StyleGrid(grid);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            grid.CellValueNeeded += delegate (object s, DataGridViewCellValueEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
                var en = _shown[e.RowIndex];
                switch (grid.Columns[e.ColumnIndex].Name)
                {
                    case "t": e.Value = en.LocalTime.ToString("HH:mm:ss"); break;
                    case "l": e.Value = en.Level; break;
                    case "m": e.Value = en.Module; break;
                    case "a": e.Value = en.Action; break;
                    case "msg": e.Value = (en.Message ?? "").Replace("\r", " ").Replace("\n", " "); break;
                    case "r": e.Value = en.Result; break;
                }
            };
            grid.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
                var en = _shown[e.RowIndex]; string n = grid.Columns[e.ColumnIndex].Name;
                if (n == "l" || n == "r")
                {
                    if (en.Level == "ERROR" || en.Level == "FATAL" || en.Result == "FAIL") e.CellStyle.ForeColor = Theme.Danger;
                    else if (en.Level == "WARN") e.CellStyle.ForeColor = Theme.Warn;
                    else if (n == "r" && en.Result == "OK") e.CellStyle.ForeColor = Theme.Success;
                    else if (en.Level == "DEBUG") e.CellStyle.ForeColor = Theme.TextMuted;
                }
                else if (n == "t" || n == "a") e.CellStyle.ForeColor = Theme.TextMuted;
                if (n == "msg" && (en.Level == "ERROR" || en.Level == "FATAL")) e.CellStyle.ForeColor = Theme.Danger;
            };
            grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _shown.Count) return;
                var en = _shown[e.RowIndex];
                FluentBox.Show(FindForm(), en.LocalTime.ToString("yyyy-MM-dd HH:mm:ss") + "   [" + en.Level + "]  " + en.Module + " / " + en.Action + "\r\n\r\n" + en.Message, "로그 자세히");
            };

            // 이전 기록(오늘/어제 jsonl) 불러오기 + 실시간 구독
            _all.AddRange(Log.LoadRecent(1000));
            Log.Entry += OnEntry;
            _flush.Tick += delegate { Flush(); };
            _flush.Start();
            txtSearch.TextChanged += delegate { Refilter(true); };
            cmbLevel.SelectedIndexChanged += delegate { Refilter(true); };
            bFolder.Click += delegate { Ui.OpenFolder(Paths.Logs); };
            bClear.Click += delegate { lock (_gate) { _all.Clear(); _pending.Clear(); } Refilter(false); };
            Refilter(false);
        }

        private void OnEntry(LogEntry e)
        {
            if (e.Action == "heartbeat") return;
            lock (_gate) _pending.Add(e);
        }

        private void Flush()
        {
            List<LogEntry> add;
            lock (_gate) { if (_pending.Count == 0) return; add = new List<LogEntry>(_pending); _pending.Clear(); }
            _all.AddRange(add);
            if (_all.Count > Max) _all.RemoveRange(0, _all.Count - Max);
            if (!Visible) { return; }
            Refilter(false);
        }

        private bool Pass(LogEntry e)
        {
            int lv = Rank(e.Level);
            switch (cmbLevel.SelectedIndex) { case 1: if (lv < 1) return false; break; case 2: if (lv < 2) return false; break; case 3: if (lv < 3) return false; break; }
            string q = txtSearch.Text.Trim();
            if (q.Length == 0) return true;
            return (e.Message ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || e.Module.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   e.Action.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || e.Level.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static int Rank(string l) { switch (l) { case "DEBUG": return 0; case "INFO": return 1; case "WARN": return 2; default: return 3; } }

        private void Refilter(bool keepTop)
        {
            _shown = _all.Where(Pass).ToList();
            int first = grid.FirstDisplayedScrollingRowIndex;
            grid.RowCount = 0; grid.RowCount = _shown.Count;
            if (swScroll.Checked && _shown.Count > 0 && !keepTop) { try { grid.FirstDisplayedScrollingRowIndex = Math.Max(0, _shown.Count - 1); } catch { } }
            else if (first >= 0 && first < _shown.Count) { try { grid.FirstDisplayedScrollingRowIndex = first; } catch { } }
            lblCount.Text = "표시 " + _shown.Count + "건 / 전체 " + _all.Count + "건  ·  행을 두 번 누르면 전체 내용을 볼 수 있습니다  ·  파일: " + Paths.Logs;
        }

        public override void OnActivated() { Flush(); Refilter(false); }
        public override void OnThemeChanged() { Theme.StyleGrid(grid); grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False; grid.Invalidate(); }
    }
}
