namespace Cthangover.Core.Localization
{
    using System;

    /// <summary>
    /// One reading of language codes for every place that gets one: the saved
    /// setting (<c>SettingsData.Language</c>, <c>game_config.json</c>), locale file
    /// suffixes, collection <c>lang</c> entries and the boot messages. Only the
    /// primary subtag counts — <c>en</c>, <c>en-en</c>, <c>en_US</c> and <c>EN</c>
    /// are the same language — because the settings have been written in all of
    /// these forms over time (the menu once saved <c>ru-ru</c>, the config ships
    /// <c>en-en</c>) and an exact match silently fell back to Russian.
    /// </summary>
    public static class LanguageCode
    {
        /// <summary>Languages the game ships locale files for, in settings menu order.</summary>
        public static readonly string[] Supported = { "ru", "en", "zh" };

        /// <summary>Lowercase primary subtag (<c>"en-US"</c> → <c>"en"</c>); null for an empty code.</summary>
        public static string Primary(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;
            var trimmed = code.Trim();
            var end = trimmed.IndexOfAny(new[] { '-', '_' });
            return (end < 0 ? trimmed : trimmed.Substring(0, end)).ToLowerInvariant();
        }

        /// <summary>The supported language <paramref name="code"/> means (one of <see cref="Supported"/>), or null.</summary>
        public static string Normalize(string code)
        {
            var primary = Primary(code);
            return primary != null && Array.IndexOf(Supported, primary) >= 0 ? primary : null;
        }

        /// <summary>True when both codes name the same language (same primary subtag).</summary>
        public static bool Same(string a, string b)
        {
            var left = Primary(a);
            return left != null && left == Primary(b);
        }
    }
}
