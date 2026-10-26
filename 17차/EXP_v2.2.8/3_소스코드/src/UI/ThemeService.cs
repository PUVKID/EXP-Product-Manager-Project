using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BunjangAuto
{
    public enum ThemeMode { Light, Dark, System }

    /// <summary>
    /// Light / Dark / System 테마를 관리한다. 선택값은 settings.json(AppSettings.ThemeMode)에 저장되어 재실행 후에도 유지된다.
    /// System 은 Windows 의 '앱 모드' 설정을 따르며, Windows 설정을 바꾸면 실행 중에도 즉시 따라간다.
    /// </summary>
    public static class ThemeService
    {
        public static ThemeMode Mode = ThemeMode.System;
        private static AppSettings _cfg;
        private static SynchronizationContext _ui;
        private static bool _hooked;

        public static ThemeMode Parse(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "light": return ThemeMode.Light;
                case "dark": return ThemeMode.Dark;
                default: return ThemeMode.System;
            }
        }

        /// <summary>Windows '앱 모드'가 어두운지. 읽을 수 없으면(Windows 10 1809 이전, 비 Windows) 밝음으로 본다.</summary>
        public static bool SystemIsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    return v != null && Convert.ToInt32(v) == 0;
                }
            }
            catch { return false; }
        }

        public static bool EffectiveDark { get { return Mode == ThemeMode.Dark || (Mode == ThemeMode.System && SystemIsDark()); } }

        public static void Init(AppSettings cfg)
        {
            _cfg = cfg;
            if (SynchronizationContext.Current != null) _ui = SynchronizationContext.Current;   // 창이 만들어진 뒤 다시 불리면 UI 스레드 컨텍스트로 갱신된다
            Mode = Parse(cfg != null ? cfg.ThemeMode : "System");
            Theme.Set(EffectiveDark ? ThemePalette.DarkPal : ThemePalette.Light);
            if (!_hooked)
            {
                _hooked = true;
                try
                {
                    SystemEvents.UserPreferenceChanged += delegate (object s, UserPreferenceChangedEventArgs e)
                    {
                        if (Mode != ThemeMode.System) return;
                        if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color && e.Category != UserPreferenceCategory.VisualStyle) return;
                        Action a = delegate { Theme.Set(EffectiveDark ? ThemePalette.DarkPal : ThemePalette.Light); };
                        if (_ui != null) _ui.Post(delegate { a(); }, null);   // UI 스레드에서만 팔레트를 바꾼다
                    };
                }
                catch { }
            }
        }

        /// <summary>사용자가 테마를 바꿨을 때: 즉시 적용 + 저장 + 로그.</summary>
        public static void SetMode(ThemeMode m)
        {
            if (m == Mode && _cfg != null && string.Equals(_cfg.ThemeMode, m.ToString(), StringComparison.OrdinalIgnoreCase)) return;
            Mode = m;
            if (_cfg != null)
            {
                _cfg.ThemeMode = m.ToString();
                try { Storage.SaveSettings(_cfg); } catch { }
            }
            Theme.Set(EffectiveDark ? ThemePalette.DarkPal : ThemePalette.Light);
            Log.Write("INFO", "Theme", "theme.change", "테마를 '" + Label(m) + "'(으)로 바꿨습니다.", "OK");
        }

        public static string Label(ThemeMode m) { return m == ThemeMode.Light ? "라이트" : m == ThemeMode.Dark ? "다크" : "시스템"; }
    }
}
