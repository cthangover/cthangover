namespace Cthangover.Core.Localization
{
    /// <summary>
    /// Built-in strings for the launcher boot overlay — the very first frame
    /// the player sees, rendered before mod discovery and compilation. Mods are
    /// the only source of locale files, and <see cref="LocaleLoader"/> merges
    /// them only after the compile phase, so during boot <c>Tr()</c> has no
    /// entries and would leak raw keys. The hint is therefore hardcoded per
    /// language (resolved from <c>GameData.Settings.Language</c> through
    /// <see cref="LanguageCode.Normalize"/>, so <c>ru</c> and <c>ru-ru</c> are both
    /// Russian) with an English fallback for unknown, missing or null codes.
    /// </summary>
    public static class BootMessages
    {
        /// <summary>
        /// Hint shown while mods compile and cache on a background thread.
        /// Unknown, missing or null language codes resolve to English instead
        /// of throwing or leaking a raw key.
        /// </summary>
        public static string FirstLaunchHint(string language)
        {
            return LanguageCode.Normalize(language) switch
            {
                "ru" => "Первый запуск может быть долгим, выполняется компиляция и кеширование...",
                "es" => "El primer arranque puede tardar: se están compilando y guardando en caché los mods...",
                "zh" => "首次启动可能需要较长时间：正在编译和缓存模组...",
                "ja" => "初回起動には時間がかかることがあります：MOD をコンパイルしてキャッシュしています...",
                "ko" => "첫 실행은 오래 걸릴 수 있습니다: 모드를 컴파일하고 캐시하는 중입니다...",
                "vi" => "Lần chạy đầu có thể lâu: đang biên dịch và lưu bộ nhớ đệm các mod...",
                _ => "The first launch may take a while: mods are being compiled and cached...",
            };
        }

        /// <summary>
        /// Title of the screen shown while the process switches its mod set
        /// (another game, hot reload). The new set's locale is not merged yet,
        /// so this cannot come from mod locale files either.
        /// </summary>
        public static string LoadingMods(string language)
        {
            return LanguageCode.Normalize(language) switch
            {
                "ru" => "Загрузка модов...",
                "es" => "Cargando mods...",
                "zh" => "正在加载模组...",
                "ja" => "MOD を読み込んでいます...",
                "ko" => "모드를 불러오는 중...",
                "vi" => "Đang tải mod...",
                _ => "Loading mods...",
            };
        }

        /// <summary>Title shown when the new mod set did not compile and the switch was cancelled.</summary>
        public static string LoadingModsFailed(string language)
        {
            return LanguageCode.Normalize(language) switch
            {
                "ru" => "Не удалось загрузить моды — ошибки компиляции:",
                "es" => "No se pudieron cargar los mods: errores de compilación:",
                "zh" => "无法加载模组 — 编译错误：",
                "ja" => "MOD を読み込めませんでした — コンパイルエラー：",
                "ko" => "모드를 불러오지 못했습니다 — 컴파일 오류:",
                "vi" => "Không tải được mod — lỗi biên dịch:",
                _ => "Could not load the mods — compile errors:",
            };
        }

        /// <summary>Button that leaves the failed switch screen.</summary>
        public static string Back(string language)
        {
            return LanguageCode.Normalize(language) switch
            {
                "ru" => "Назад",
                "es" => "Atrás",
                "zh" => "返回",
                "ja" => "戻る",
                "ko" => "뒤로",
                "vi" => "Quay lại",
                _ => "Back",
            };
        }
    }
}
