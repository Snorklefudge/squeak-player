using System.Globalization;

namespace SqueakPlayer
{
    public enum AppLang { English, Polish }

    /// <summary>
    /// Tiny in-code localization. English is the default; Polish is used when the
    /// user picks it or (in "auto" mode) when Windows' display language is Polish.
    /// </summary>
    public static class Loc
    {
        public static AppLang Cur = AppLang.English;
        private static bool PL => Cur == AppLang.Polish;

        public static AppLang Resolve(string? pref) => pref switch
        {
            "pl" => AppLang.Polish,
            "en" => AppLang.English,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl"
                    ? AppLang.Polish : AppLang.English
        };

        public static string DropHint => PL ? "Przeciągnij plik wideo tutaj albo Ctrl+O" : "Drag a video file here, or press Ctrl+O";
        public static string SkipIntro => PL ? "Pomiń intro" : "Skip intro";

        public static string TipMute => PL ? "Wycisz (M)" : "Mute (M)";
        public static string TipPrev => PL ? "Poprzedni w folderze" : "Previous in folder";
        public static string TipNext => PL ? "Następny w folderze" : "Next in folder";
        public static string TipOpen => PL ? "Otwórz plik (Ctrl+O)" : "Open file (Ctrl+O)";
        public static string TipFull => PL ? "Pełny ekran (F)" : "Fullscreen (F)";
        public static string TipMin => PL ? "Minimalizuj" : "Minimize";
        public static string TipClose => PL ? "Zamknij" : "Close";
        public static string TipPin => PL ? "Przypnij na wierzchu (T)" : "Pin on top (T)";
        public static string TipUnpin => PL ? "Odepnij (T)" : "Unpin (T)";

        public static string MenuPrev => PL ? "Poprzedni" : "Previous";
        public static string MenuNext => PL ? "Następny" : "Next";
        public static string MenuAudio => PL ? "Ścieżka audio" : "Audio track";
        public static string MenuSubs => PL ? "Napisy" : "Subtitles";
        public static string MenuAutoplay => PL ? "Autoodtwarzanie" : "Autoplay";
        public static string MenuOnTop => PL ? "Na wierzchu" : "Always on top";
        public static string MenuFull => PL ? "Pełny ekran" : "Fullscreen";
        public static string MenuOpen => PL ? "Otwórz plik…" : "Open file…";
        public static string MenuLang => PL ? "Język" : "Language";
        public static string LangAuto => PL ? "Automatycznie" : "Automatic";

        public static string Muted => PL ? "🔇  Wyciszony" : "🔇  Muted";
        public static string ErrorTitle => PL ? "Squeak – błąd" : "Squeak – Error";

        public static string FileFilter => PL
            ? "Wideo|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|Wszystkie pliki|*.*"
            : "Video|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|All files|*.*";

        public static string Chapter(int cur, int count) => PL ? $"Rozdział {cur}/{count}" : $"Chapter {cur}/{count}";
    }
}
