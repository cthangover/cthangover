using Cthangover.Core.Localization;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Implements the collection text convention: a <c>name</c>,
    /// <c>description</c>, include <c>label</c>/<c>note</c> (or a locale entry)
    /// that starts with <c>'@'</c> is a translation key (e.g.
    /// <c>"@collections/novel_template/name"</c>), registered by
    /// <see cref="LocaleLoader"/> from the <c>locale/*.properties</c> files of
    /// every mod. The remainder after <c>'@'</c> is looked up through Godot's
    /// <c>TranslationServer</c>; when no translation is registered the raw text
    /// after <c>'@'</c> is shown instead, so an untranslated collection never
    /// renders as a bare key or an empty string.
    /// <para/>
    /// The class is the Godot-dependent half of the convention:
    /// <see cref="StripKey"/> stays pure for tests and headless tools, while
    /// <see cref="Resolve"/> is what the collections screen passes to
    /// <see cref="ModCollectionResolver"/> as its <c>translator</c> argument.
    /// </summary>
    public static class CollectionText
    {
        /// <summary>Prefix marking a field value as a translation key.</summary>
        private const char KeyPrefix = '@';

        /// <summary>
        /// True when the raw field value is a translation-key reference
        /// (starts with <c>'@'</c>).
        /// </summary>
        public static bool IsKey(string raw)
            => !string.IsNullOrEmpty(raw) && raw[0] == KeyPrefix;

        /// <summary>
        /// Translation key without the leading <c>'@'</c>, or null when the
        /// value is not a key reference. Pure — safe outside the Godot runtime.
        /// </summary>
        public static string StripKey(string raw)
            => IsKey(raw) ? raw.Substring(1) : null;

        /// <summary>
        /// Resolves a raw field value for display: plain text is returned
        /// unchanged, an <c>'@'</c>-prefixed value is translated through the
        /// Godot <c>TranslationServer</c>, falling back to the stripped key
        /// text when the key is not registered (or when no Godot runtime is
        /// available, e.g. in unit tests). Never returns null for non-null
        /// input, so UI code can bind the result directly.
        /// </summary>
        public static string Resolve(string raw)
        {
            var key = StripKey(raw);
            if (key == null)
                return raw;

            if (string.IsNullOrEmpty(key) || !GameEnvironment.IsRuntime)
                return key;

            try
            {
                // The provider returns null when Godot echoes the key back,
                // which is its signal for "no translation registered".
                return GodotTranslationProvider.Instance.Get(key) ?? key;
            }
            catch
            {
                // Godot API unavailable on this thread or not initialized —
                // degrade gracefully to the human-readable key text.
                return key;
            }
        }
    }
}
