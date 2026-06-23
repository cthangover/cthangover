using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Localization
{
    public static class LocaleLoader
    {
        private static readonly Dictionary<string, string> LangToLocale = new()
        {
            { "ru-ru", "ru" },
            { "en", "en" },
            { "zh", "zh" },
        };

        private static readonly Dictionary<string, Translation> _translations = new();

        public static void LoadCurrentLanguage()
        {
            var settingsLang = GameData.Instance.Settings.Language;
            if (!LangToLocale.TryGetValue(settingsLang, out var locale))
                locale = "ru";

            LoadLocale(locale);
        }

        public static void LoadLocale(string locale)
        {
            var master = new Dictionary<string, string>();

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

        private static void MergeModLocales(string locale, Dictionary<string, string> master)
        {
            var suffix = $"_{locale}.properties";
            var exactName = $"{locale}.properties";

            ModManager.Instance.Initialize();

            foreach (var kvp in ModManager.Instance.Mods)
            {
                var provider = kvp.Value.FileProvider;

                var localeFiles = provider.ListFiles("locale").ToList();
                foreach (var filePath in localeFiles)
                {
                    if (filePath.EndsWith("/"))
                        continue;

                    var fileName = System.IO.Path.GetFileName(filePath);
                    if (fileName == exactName || fileName.EndsWith(suffix))
                        LoadPropertiesFromMod(provider, filePath, master);
                }
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
