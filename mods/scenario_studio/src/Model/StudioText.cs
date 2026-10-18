namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Player-facing text of the studio model kept as a localization key plus
    /// format arguments and translated only when shown, so the dock follows a
    /// language switch and no prose lives in code. Arguments may themselves be
    /// <see cref="StudioText"/> (e.g. a scenario's own <c>key=</c> inside a
    /// studio phrase) and are resolved recursively; plain arguments are data —
    /// scene, scenario, mod and quest ids, numbers. <see cref="Raw"/> wraps such
    /// data where a text is expected.
    /// </summary>
    public sealed class StudioText
    {
        private readonly string _raw;

        public string Key { get; }
        public object[] Args { get; }

        public StudioText(string key, params object[] args)
        {
            Key = key;
            Args = args ?? System.Array.Empty<object>();
        }

        private StudioText(string raw, bool _)
        {
            _raw = raw;
            Args = System.Array.Empty<object>();
        }

        /// <summary>Untranslated data (an id or a number) used where a text is expected.</summary>
        public static StudioText Raw(string value) => new(value, true);

        public string Resolve()
        {
            if (Key == null)
                return _raw ?? string.Empty;

            var format = TranslationServer.Translate(Key).ToString();
            if (Args.Length == 0)
                return format;

            var resolved = new object[Args.Length];
            for (var i = 0; i < Args.Length; i++)
                resolved[i] = Args[i] is StudioText text ? text.Resolve() : Args[i];
            try
            {
                return string.Format(format, resolved);
            }
            catch (System.FormatException)
            {
                GameLogger.Log("UI", $"Scenario studio: '{Key}' expects other arguments than {Args.Length}", LogLevel.Warning);
                return format;
            }
        }
    }
}
