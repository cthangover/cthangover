using Godot;

namespace Cthangover.Core.Localization
{
    /// <summary>
    /// Singleton adapter that wraps Godot's built-in
    /// <c>TranslationServer.Translate</c>. Returns <c>null</c> when the
    /// translation key is returned verbatim (Godot's signal for "no
    /// translation found"), enabling callers to detect missing entries
    /// and substitute a fallback instead of leaking raw keys into the UI.
    /// </summary>
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
