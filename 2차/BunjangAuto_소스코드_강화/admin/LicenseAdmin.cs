using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using BunjangAuto;

static class AdminProgram
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new AdminForm());
    }
}

class AdminForm : Form
{
    string _priv = "";
    Label lblKey;
    TextBox txtName, txtMid, txtOut;
    DateTimePicker dtp;
    NumericUpDown numDays;
    bool _sync;
    CheckBox chkForever;
    string Dir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
    string PrivPath { get { return Path.Combine(Dir, "license_private.xml"); } }

    public AdminForm()
    {
        Text = "번개장터 자동등록 - 라이선스 발급 (관리자 전용)";
        Width = 600; Height = 540;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Malgun Gothic", 9f);

        lblKey = new Label { Left = 16, Top = 12, Width = 560, Height = 34 };
        var l1 = new Label { Left = 16, Top = 56, AutoSize = true, Text = "사용자 이름 / 메모" };
        txtName = new TextBox { Left = 16, Top = 78, Width = 550 };
        var l2 = new Label { Left = 16, Top = 112, AutoSize = true, Text = "사용자 PC 고유번호 (인증 창에 표시된 XXXX-XXXX-XXXX)" };
        txtMid = new TextBox { Left = 16, Top = 134, Width = 550, Font = new Font("Consolas", 11f) };
        var l3 = new Label { Left = 16, Top = 172, AutoSize = true, Text = "사용 기간 (만료일 또는 일수 입력, 만료일 당일 23:59까지 사용)" };
        dtp = new DateTimePicker { Left = 16, Top = 194, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(30) };
        chkForever = new CheckBox { Left = 190, Top = 196, AutoSize = true, Text = "무기한" };
        numDays = new NumericUpDown { Left = 300, Top = 192, Width = 70, Minimum = 1, Maximum = 3650, Value = 30 };
        var lblDays = new Label { Left = 374, Top = 196, AutoSize = true, Text = "일 (오늘부터)" };
        var b7 = new Button { Left = 16, Top = 222, Width = 60, Text = "7일" };
        var b30 = new Button { Left = 80, Top = 222, Width = 60, Text = "30일" };
        var b90 = new Button { Left = 144, Top = 222, Width = 60, Text = "90일" };
        var b365 = new Button { Left = 208, Top = 222, Width = 60, Text = "1년" };
        var bGen = new Button { Left = 16, Top = 256, Width = 550, Height = 34, Text = "라이선스 키 발급" };
        var l4 = new Label { Left = 16, Top = 300, AutoSize = true, Text = "발급된 키 (사용자에게 그대로 전달)" };
        txtOut = new TextBox { Left = 16, Top = 322, Width = 550, Height = 120, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
        var bCopy = new Button { Left = 16, Top = 452, Width = 130, Height = 30, Text = "키 복사" };
        Controls.AddRange(new Control[] { lblKey, l1, txtName, l2, txtMid, l3, dtp, chkForever, numDays, lblDays, b7, b30, b90, b365, bGen, l4, txtOut, bCopy });

        LoadKey();
        chkForever.CheckedChanged += delegate { dtp.Enabled = !chkForever.Checked; numDays.Enabled = !chkForever.Checked; };
        numDays.ValueChanged += delegate { if (_sync) return; _sync = true; dtp.Value = DateTime.Today.AddDays((double)numDays.Value); _sync = false; };
        dtp.ValueChanged += delegate
        {
            if (_sync) return; _sync = true;
            int d = (int)(dtp.Value.Date - DateTime.Today).TotalDays;
            numDays.Value = Math.Max(numDays.Minimum, Math.Min(numDays.Maximum, d));
            _sync = false;
        };
        b7.Click += delegate { numDays.Value = 7; chkForever.Checked = false; };
        b30.Click += delegate { numDays.Value = 30; chkForever.Checked = false; };
        b90.Click += delegate { numDays.Value = 90; chkForever.Checked = false; };
        b365.Click += delegate { numDays.Value = 365; chkForever.Checked = false; };
        bCopy.Click += delegate { if (txtOut.Text.Length > 0) Clipboard.SetText(txtOut.Text); };
        bGen.Click += delegate
        {
            if (_priv.Length == 0) { MessageBox.Show(this, "license_private.xml(개인키)이 없습니다."); return; }
            if (txtName.Text.Trim().Length == 0) { MessageBox.Show(this, "사용자 이름을 입력해 주세요."); return; }
            string mid = txtMid.Text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(mid, "^[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}$")) { MessageBox.Show(this, "PC 고유번호 형식이 올바르지 않습니다. (예: 9CB2-7594-3C5E)"); return; }
            try
            {
                string payload = License.Payload(txtName.Text.Trim(), mid, chkForever.Checked ? (DateTime?)null : dtp.Value.Date);
                string key = License.Sign(_priv, payload);
                txtOut.Text = key;
                File.AppendAllText(Path.Combine(Dir, "issued_licenses.csv"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "," + txtName.Text.Trim().Replace(",", " ") + "," + (mid.Length == 0 ? "*" : mid) + "," +
                    (chkForever.Checked ? "무기한" : dtp.Value.ToString("yyyy-MM-dd")) + "\r\n", new UTF8Encoding(true));
            }
            catch (Exception ex) { MessageBox.Show(this, "발급 실패: " + ex.Message); }
        };
    }

    void LoadKey()
    {
        if (File.Exists(PrivPath))
        {
            _priv = File.ReadAllText(PrivPath, Encoding.UTF8);
            lblKey.ForeColor = Color.DarkGreen;
            lblKey.Text = "개인키 로드됨: " + PrivPath + "\r\n이 파일은 절대 배포하지 마세요.";
        }
        else
        {
            lblKey.ForeColor = Color.Firebrick;
            lblKey.Text = "license_private.xml 이 이 프로그램과 같은 폴더에 없습니다.";
        }
    }
}
