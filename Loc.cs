using System.Globalization;

namespace SqueakPlayer
{
    public enum AppLang { English, Polish, Spanish, French, Chinese, Japanese }

    /// <summary>
    /// Tiny in-code localization. English is the default; another language is used
    /// when the user picks it or (in "auto" mode) when Windows' display language
    /// matches. Each string lists its translations in the order of <see cref="AppLang"/>.
    /// </summary>
    public static class Loc
    {
        public static AppLang Cur = AppLang.English;

        // Pick the phrase for the current language, falling back to English.
        private static string T(string en, string pl, string es, string fr, string zh, string ja) => Cur switch
        {
            AppLang.Polish => pl,
            AppLang.Spanish => es,
            AppLang.French => fr,
            AppLang.Chinese => zh,
            AppLang.Japanese => ja,
            _ => en,
        };

        private static AppLang FromCode(string? code) => code switch
        {
            "pl" => AppLang.Polish,
            "es" => AppLang.Spanish,
            "fr" => AppLang.French,
            "zh" => AppLang.Chinese,
            "ja" => AppLang.Japanese,
            _ => AppLang.English,
        };

        public static AppLang Resolve(string? pref) => pref switch
        {
            "pl" or "es" or "fr" or "zh" or "ja" or "en" => FromCode(pref),
            _ => FromCode(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
        };

        public static string DropHint => T(
            "Drag a video file here, or press Ctrl+O",
            "Przeciągnij plik wideo tutaj albo Ctrl+O",
            "Arrastra un archivo de vídeo aquí o pulsa Ctrl+O",
            "Glissez un fichier vidéo ici, ou appuyez sur Ctrl+O",
            "将视频文件拖到此处，或按 Ctrl+O",
            "ここに動画ファイルをドラッグ、または Ctrl+O");
        public static string SkipIntro => T("Skip intro", "Pomiń intro", "Saltar intro", "Passer l'intro", "跳过片头", "イントロをスキップ");

        public static string TipMute => T("Mute (M)", "Wycisz (M)", "Silenciar (M)", "Muet (M)", "静音 (M)", "ミュート (M)");
        public static string TipPrev => T("Previous in folder", "Poprzedni w folderze", "Anterior en la carpeta", "Précédent dans le dossier", "文件夹中的上一个", "フォルダー内の前へ");
        public static string TipNext => T("Next in folder", "Następny w folderze", "Siguiente en la carpeta", "Suivant dans le dossier", "文件夹中的下一个", "フォルダー内の次へ");
        public static string TipOpen => T("Open file (Ctrl+O)", "Otwórz plik (Ctrl+O)", "Abrir archivo (Ctrl+O)", "Ouvrir un fichier (Ctrl+O)", "打开文件 (Ctrl+O)", "ファイルを開く (Ctrl+O)");
        public static string TipFull => T("Fullscreen (F)", "Pełny ekran (F)", "Pantalla completa (F)", "Plein écran (F)", "全屏 (F)", "全画面 (F)");
        public static string TipMin => T("Minimize", "Minimalizuj", "Minimizar", "Réduire", "最小化", "最小化");
        public static string TipClose => T("Close", "Zamknij", "Cerrar", "Fermer", "关闭", "閉じる");
        public static string TipPin => T("Pin on top (T)", "Przypnij na wierzchu (T)", "Fijar encima (T)", "Épingler au-dessus (T)", "置顶 (T)", "最前面に固定 (T)");
        public static string TipUnpin => T("Unpin (T)", "Odepnij (T)", "Desfijar (T)", "Détacher (T)", "取消置顶 (T)", "固定を解除 (T)");

        public static string MenuPrev => T("Previous", "Poprzedni", "Anterior", "Précédent", "上一个", "前へ");
        public static string MenuNext => T("Next", "Następny", "Siguiente", "Suivant", "下一个", "次へ");
        public static string MenuAudio => T("Audio track", "Ścieżka audio", "Pista de audio", "Piste audio", "音轨", "音声トラック");
        public static string MenuSubs => T("Subtitles", "Napisy", "Subtítulos", "Sous-titres", "字幕", "字幕");
        public static string MenuAutoplay => T("Autoplay", "Autoodtwarzanie", "Reproducción automática", "Lecture automatique", "自动播放", "自動再生");
        public static string MenuOnTop => T("Always on top", "Na wierzchu", "Siempre encima", "Toujours au-dessus", "始终置顶", "常に最前面");
        public static string MenuFull => T("Fullscreen", "Pełny ekran", "Pantalla completa", "Plein écran", "全屏", "全画面");
        public static string MenuOpen => T("Open file…", "Otwórz plik…", "Abrir archivo…", "Ouvrir un fichier…", "打开文件…", "ファイルを開く…");
        public static string MenuLang => T("Language", "Język", "Idioma", "Langue", "语言", "言語");
        public static string LangAuto => T("Automatic", "Automatycznie", "Automático", "Automatique", "自动", "自動");

        public static string Muted => T("🔇  Muted", "🔇  Wyciszony", "🔇  Silenciado", "🔇  Muet", "🔇  已静音", "🔇  ミュート");
        public static string ErrorTitle => T("Squeak – Error", "Squeak – błąd", "Squeak – Error", "Squeak – Erreur", "Squeak – 错误", "Squeak – エラー");

        public static string FileFilter => T(
            "Video|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|All files|*.*",
            "Wideo|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|Wszystkie pliki|*.*",
            "Vídeo|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|Todos los archivos|*.*",
            "Vidéo|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|Tous les fichiers|*.*",
            "视频|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|所有文件|*.*",
            "動画|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.ts|すべてのファイル|*.*");

        public static string Chapter(int cur, int count) => T(
            $"Chapter {cur}/{count}",
            $"Rozdział {cur}/{count}",
            $"Capítulo {cur}/{count}",
            $"Chapitre {cur}/{count}",
            $"章节 {cur}/{count}",
            $"チャプター {cur}/{count}");
    }
}
