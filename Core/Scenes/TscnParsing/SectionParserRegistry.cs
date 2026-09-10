using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>Auto-discovering registry of <see cref="ISectionParser"/> implementations, indexed by SectionType. First-registered-wins.</summary>
    public static class SectionParserRegistry
    {
        private static System.Collections.Generic.Dictionary<string, ISectionParser> _parsers;

        public static void Initialize()
        {
            if (_parsers != null) return;
            _parsers = new System.Collections.Generic.Dictionary<string, ISectionParser>();
            foreach (var parser in Reflections.FindAndCreate<ISectionParser>())
                if (!_parsers.ContainsKey(parser.SectionType))
                    _parsers[parser.SectionType] = parser;
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            Initialize();
            foreach (var parser in Reflections.FindAndCreate<ISectionParser>(assembly))
                if (!_parsers.ContainsKey(parser.SectionType))
                    _parsers[parser.SectionType] = parser;
        }

        public static ISectionParser Get(string sectionType) { Initialize(); _parsers.TryGetValue(sectionType, out var p); return p; }
    }
}
