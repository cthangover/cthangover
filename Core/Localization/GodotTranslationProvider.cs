using Godot;

namespace Cthangover.Core.Localization
{
    public class GodotTranslationProvider : ILocalizationProvider
    {
        public static readonly GodotTranslationProvider Instance = new();

        public string Get(string key)
        {
            var result = TranslationServer.Translate(key);
            return result != key ? result : null;
        }
    }
}
