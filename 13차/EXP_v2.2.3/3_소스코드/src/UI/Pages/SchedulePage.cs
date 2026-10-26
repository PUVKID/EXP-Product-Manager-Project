using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BunjangAuto
{
    /// <summary>예약: 1개씩 등록 간격 / 시간대별 / 중복 허용 / 반복. 예전 '실행/예약' 탭의 설정을 그대로 옮겼고 값이 바뀌면 바로 저장된다.</summary>
    public class SchedulePage : PageBase
    {
        private readonly ToggleSwitch chkRegister, chkOneByOne, chkWindows, chkDup, chkRepeat;
        private readonly NumericUpDown numGap, numRounds;
        private readonly DataGridView gridRules;
        private readonly FluentButton bAdd, bDel;
        private readonly Label lblGap, lblRounds, lblRulesHint;
        private bool _loading;

        public SchedulePage(AppHost host) : base(host)
        {
            Key = "schedule"; PageTitle = "예약";
            var title = Ui.Title("예약"); title.Location = new Point(0, 0);
            var sub = Ui.Muted("반복·시간대 예약: Studio / Pro · 번개장터 서버 시각 동기화 후 실행됩니다."); sub.Location = new Point(2, 38);

            var card = new CardPanel { Location = new Point(0, 74), Height = 560, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            int y = 18;
            var h1 = Ui.Heading("실행할 작업"); h1.Location = new Point(20, y); y += 34;
            chkRegister = Ui.Toggle("등록 — 상품 목록의 상품을 올립니다"); chkRegister.Location = new Point(20, y); y += 44;

            var h2 = Ui.Heading("1개씩 등록"); h2.Location = new Point(20, y); y += 34;
            chkOneByOne = Ui.Toggle("1개씩 등록하고, 모두 올리면 자동 종료"); chkOneByOne.Location = new Point(20, y); y += 36;
            lblGap = Ui.Muted("기본 간격(분)"); lblGap.Location = new Point(60, y + 4);
            numGap = Ui.Number(1, 1440, 90); numGap.Location = new Point(170, y); y += 40;
            chkWindows = Ui.Toggle("시간대별로 간격 지정 (켜면 위 기본 간격 대신 아래 표를 사용)"); chkWindows.Location = new Point(60, y); y += 36;

            gridRules = new DataGridView
            {
                Location = new Point(60, y), Size = new Size(520, 130), AutoGenerateColumns = false, AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, MultiSelect = false
            };
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Start", HeaderText = "시작 (HH:mm)", Width = 150 });
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "End", HeaderText = "종료 (HH:mm)", Width = 150 });
            gridRules.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Minutes", HeaderText = "간격(분)", Width = 120 });
            gridRules.DataSource = Host.Auto.Rules;
            y += 138;
            bAdd = Ui.Button("시간대 추가", "\uE710", BtnStyle.Standard); bAdd.Location = new Point(60, y);
            bDel = Ui.Button("선택 삭제", "\uE74D", BtnStyle.Standard); bDel.Location = new Point(190, y);
            lblRulesHint = Ui.Muted("한국시간 기준 · 새 시간대 시작 시 대기 초기화 · 겹치면 나중에 시작한 시간대 우선"); lblRulesHint.Location = new Point(60, y + 40);
            y += 82;

            chkDup = Ui.Toggle("중복 등록 허용 (이미 등록된 상품도 다시 올림)"); chkDup.Location = new Point(20, y); y += 44;

            var h3 = Ui.Heading("반복"); h3.Location = new Point(20, y); y += 34;
            chkRepeat = Ui.Toggle("목록을 다 올리면 처음 상품부터 다시 올리기 (1개씩 등록일 때)"); chkRepeat.Location = new Point(20, y); y += 36;
            lblRounds = Ui.Muted("총 횟수 (처음 1회 포함)"); lblRounds.Location = new Point(60, y + 4);
            numRounds = Ui.Number(2, 999, 80); numRounds.Location = new Point(240, y);

            card.Height = y + 62;     // 마지막 줄(반복 횟수)까지 들어가도록 카드 높이를 내용에 맞춘다
            card.Controls.AddRange(new Control[] { h1, chkRegister, h2, chkOneByOne, lblGap, numGap, chkWindows, gridRules, bAdd, bDel, lblRulesHint, chkDup, h3, chkRepeat, lblRounds, numRounds });
            Controls.AddRange(new Control[] { title, sub, card });
            Resize += delegate { card.Width = Math.Max(Ui.S(400), ClientSize.Width - Padding.Horizontal); };

            LoadFromSettings();
            EventHandler save = delegate { if (_loading) return; ReadToSettings(); UpdateEnabled(); };
            foreach (var t in new[] { chkRegister, chkOneByOne, chkWindows, chkDup, chkRepeat }) t.CheckedChanged += save;
            numGap.ValueChanged += save; numRounds.ValueChanged += save;
            gridRules.CellEndEdit += delegate { if (!_loading) { Host.SaveSettings(); } };
            bAdd.Click += delegate { Host.Auto.Rules.Add(new TimeRule("00:00", "24:00", 60)); Host.SaveSettings(); };
            bDel.Click += delegate
            {
                if (gridRules.CurrentRow != null && gridRules.CurrentRow.Index >= 0 && gridRules.CurrentRow.Index < Host.Auto.Rules.Count)
                { Host.Auto.Rules.RemoveAt(gridRules.CurrentRow.Index); Host.SaveSettings(); }
            };
            Theme.StyleGrid(gridRules);
        }

        private void LoadFromSettings()
        {
            _loading = true;
            var c = Host.Cfg;
            chkRegister.Checked = c.DoRegister; chkRepeat.Checked = c.RepeatAll; numRounds.Value = Clamp(c.RepeatCount, numRounds);
            chkOneByOne.Checked = c.OneByOne; chkDup.Checked = c.AllowDuplicate; numGap.Value = Clamp(c.OneByOneMinutes, numGap);
            chkWindows.Checked = c.UseTimeRules;
            _loading = false;
            UpdateEnabled();
        }

        private static decimal Clamp(int v, NumericUpDown n) { return Math.Max(n.Minimum, Math.Min(n.Maximum, v)); }

        private void ReadToSettings()
        {
            var c = Host.Cfg;
            bool oldOne = c.OneByOne, oldWin = c.UseTimeRules, oldDup = c.AllowDuplicate, oldRep = c.RepeatAll; int oldGap = c.OneByOneMinutes, oldRounds = c.RepeatCount;
            c.DoRegister = chkRegister.Checked; c.RepeatAll = chkRepeat.Checked; c.RepeatCount = (int)numRounds.Value;
            c.OneByOne = chkOneByOne.Checked; c.AllowDuplicate = chkDup.Checked; c.OneByOneMinutes = (int)numGap.Value; c.UseTimeRules = chkWindows.Checked;
            Host.SaveSettings();
            if (oldOne != c.OneByOne || oldWin != c.UseTimeRules || oldDup != c.AllowDuplicate || oldRep != c.RepeatAll || oldGap != c.OneByOneMinutes || oldRounds != c.RepeatCount)
                Log.Write("INFO", "Settings", "settings.change", "예약 설정을 바꿨습니다. (1개씩=" + (c.OneByOne ? "켬" : "끔") + ", 간격=" + c.OneByOneMinutes + "분, 시간대별=" + (c.UseTimeRules ? "켬" : "끔") +
                          ", 반복=" + (c.RepeatAll ? c.RepeatCount + "회" : "끔") + ", 중복허용=" + (c.AllowDuplicate ? "켬" : "끔") + ")", "OK");
        }

        /// <summary>예전 UpdateRunEnabled 와 같은 규칙: 반복은 1개씩일 때만, 시간대는 1개씩일 때만, 기본 간격은 시간대를 안 쓸 때만</summary>
        private void UpdateEnabled()
        {
            bool one = chkOneByOne.Checked;
            var li=LicenseGate.LastInfo; bool advanced=li!=null && li.Valid && Lumivolt.Security.EditionPolicy.CanRepeat(li.Edition);
            chkDup.Enabled=advanced;
            chkWindows.Enabled = one && advanced;
            numGap.Enabled = one && !chkWindows.Checked; lblGap.Enabled = numGap.Enabled;
            gridRules.Enabled = one && advanced && chkWindows.Checked; bAdd.Enabled = gridRules.Enabled; bDel.Enabled = gridRules.Enabled;
            chkRepeat.Enabled = one && advanced;
            numRounds.Enabled = one && advanced && chkRepeat.Checked; lblRounds.Enabled = numRounds.Enabled;
            gridRules.DefaultCellStyle.ForeColor = gridRules.Enabled ? Theme.TextPrimary : Theme.TextMuted;
            chkWindows.Invalidate(); chkRepeat.Invalidate();
        }

        public override void OnActivated() { LoadFromSettings(); }
        public override void OnThemeChanged() { Theme.StyleGrid(gridRules); UpdateEnabled(); }
    }
}
