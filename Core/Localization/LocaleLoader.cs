using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Localization
{
    /// <summary>
    /// Static loader that assembles Godot <c>Translation</c> resources
    /// from <c>.properties</c> files distributed across mods. Chose the
    /// <c>.properties</c> format (plain <c>key=value</c>) over Godot's
    /// CSV translation format because it requires no editor round-trip
    /// and is trivially editable by mod authors in any text editor.
    ///
    /// Merges entries from <b>all</b> loaded mods into a single
    /// <c>Translation</c> — later values overwrite earlier ones via
    /// <c>Dictionary</c> assignment — so mods can selectively override
    /// base game strings without copying the entire locale file.
    /// File matching is by suffix (<c>_{locale}.properties</c>) allowing
    /// mods to organise translations however they like as long as the
    /// filename ends with the correct locale code.
    ///
    /// On locale switch, the previous <c>Translation</c> is removed from
    /// <c>TranslationServer</c> and disposed to avoid leaking Godot
    /// resource handles across language changes.
    /// </summary>
    public static class LocaleLoader
    {
        private static readonly Dictionary<string, Translation> _translations = new();

        /// <summary>
        /// Reads the language saved in <see cref="GameData.Settings"/>, reads it
        /// through <see cref="LanguageCode.Normalize"/> (<c>en-en</c>, <c>en_US</c>
        /// and <c>en</c> are all English; an unsupported one falls back to
        /// <c>"ru"</c> with a warning), and delegates to <see cref="LoadLocale"/>.
        /// </summary>
        public static void LoadCurrentLanguage()
        {
            var settingsLang = GameData.Instance.Settings.Language;
            var locale = LanguageCode.Normalize(settingsLang);
            if (locale == null)
            {
                GameLogger.Log("LANG", $"unsupported language '{settingsLang}' in the settings, using 'ru'", LogLevel.Warning);
                locale = "ru";
            }

            LoadLocale(locale);
        }

        /// <summary>
        /// Merges the <c>.properties</c> files of every loaded mod for the given
        /// locale into one Godot <see cref="Translation"/>, replaces any
        /// previous translation for that locale on <c>TranslationServer</c>, and
        /// sets it as the active locale. Safe to call again to switch languages.
        /// </summary>
        public static void LoadLocale(string locale)
        {
            var master = new Dictionary<string, string>();

            MergeCollectionLocales(locale, master);
            MergeModLocales(locale, master);

            if (_translations.TryGetValue(locale, out var old))
            {
                TranslationServer.RemoveTranslation(old);
                old.Dispose();
                _translations.Remove(locale);
            }

            var translation = new Translation();
            translation.Locale = locale;

            foreach (var kv in master)
                translation.AddMessage(kv.Key, kv.Value);

            TranslationServer.AddTranslation(translation);
            TranslationServer.SetLocale(locale);
            _translations[locale] = translation;

            GameLogger.Log("LANG", $"Loaded {master.Count} strings for locale '{locale}' from mods");
        }

        /// <summary>
        /// Texts of collection packages (<c>collections/&lt;id&gt;/locale/</c>):
        /// names and descriptions a game picker shows before the game's mods are
        /// loaded. Merged before the mods, so a mod may still override a key.
        /// </summary>
        private static void MergeCollectionLocales(string locale, Dictionary<string, string> master)
        {
            foreach (var collection in ModCollectionStore.LoadAll())
            {
                if (collection.Package != null)
                    MergeLocaleFiles(collection.Package, locale, master);
            }
        }

        private static void MergeLocaleFiles(IModFileProvider provider, string locale, Dictionary<string, string> master)
        {
            var suffix = $"_{locale}.properties";
            var exactName = $"{locale}.properties";
            foreach (var filePath in provider.ListFiles("locale").ToList())
            {
                if (filePath.EndsWith("/"))
                    continue;

                var fileName = System.IO.Path.GetFileName(filePath);
                if (fileName == exactName || fileName.EndsWith(suffix))
                    LoadPropertiesFromMod(provider, filePath, master);
            }
        }

        private static void MergeModLocales(string locale, Dictionary<string, string> master)
        {
            ModManager.Instance.Initialize();

            foreach (var modId in ModRegistry.Instance.GetOrderedModIds())
            {
                if (!ModManager.Instance.Mods.TryGetValue(modId, out var modInfo))
                    continue;
                MergeLocaleFiles(modInfo.FileProvider, locale, master);
            }
        }

        private static void LoadPropertiesFromMod(IModFileProvider provider, string path, Dictionary<string, string> target)
        {
            var text = provider.ReadFileText(path);
            if (string.IsNullOrEmpty(text))
                return;

            var count = 0;
            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                var eqPos = line.IndexOf('=');
                if (eqPos < 0)
                    continue;

                var key = line.Substring(0, eqPos).Trim();
                var value = line.Substring(eqPos + 1).Trim();

                if (key.Length > 0)
                {
                    target[key] = value;
                    count++;
                }
            }

            if (count > 0)
            {
                GameLogger.Log("LANG", $"Merged '{provider.Mod}' -> {count} keys from '{path}'", LogLevel.Debug);
            }
        }
    }
}
