namespace Cthangover.Core.Localization
{
    /// <summary>
    /// Built-in strings for the launcher boot overlay — the very first frame
    /// the player sees, rendered before mod discovery and compilation. Mods are
    /// the only source of locale files, and <see cref="LocaleLoader"/> merges
    /// them only after the compile phase, so during boot <c>Tr()</c> has no
    /// entries and would leak raw keys. The hint is therefore hardcoded per
    /// language (resolved from <c>GameData.Settings.Language</c>) with an
    /// English fallback for unknown, missing or null language codes.
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
            return language switch
            {
                "ru-ru" => "Первый запуск может быть долгим, выполняется компиляция и кеширование...",
                "zh" => "首次启动可能需要较长时间：正在编译和缓存模组...",
                _ => "The first launch may take a while: mods are being compiled and cached...",
            };
        }
    }
}
