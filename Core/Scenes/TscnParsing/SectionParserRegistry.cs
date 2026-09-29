using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Auto-discovering registry of <see cref="ISectionParser"/> implementations, indexed by
    /// SectionType. First-registered-wins; Roslyn-compiled mod assemblies can add section types
    /// via <see cref="RegisterAssembly"/>.
    /// <para/>
    /// The registry stores parser <b>types</b>, not instances, and <see cref="Get"/> creates a
    /// fresh parser per section. Parsers carry per-section state between <c>Open</c>, the
    /// property lines and <c>Close</c>; sharing one instance broke as soon as two parses ran
    /// concurrently (parallel unit tests, background scene loads), because one parse could
    /// null out another's current parser mid-section. Instantiating per call keeps
    /// <see cref="TscnParser"/> stateless and therefore safe to use from parallel threads.
    /// </summary>
    public static class SectionParserRegistry
    {
        private static readonly object Sync = new();
        private static Dictionary<string, Type> _parsers;

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_parsers != null)
                    return;

                var parsers = new Dictionary<string, Type>();
                foreach (var parser in Reflections.FindAndCreate<ISectionParser>())
                {
                    if (!parsers.ContainsKey(parser.SectionType))
                        parsers[parser.SectionType] = parser.GetType();
                }

                _parsers = parsers;
            }
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            Initialize();

            lock (Sync)
            {
                foreach (var parser in Reflections.FindAndCreate<ISectionParser>(assembly))
                {
                    if (!_parsers.ContainsKey(parser.SectionType))
                        _parsers[parser.SectionType] = parser.GetType();
                }
            }
        }

        public static ISectionParser Get(string sectionType)
        {
            Initialize();

            lock (Sync)
            {
                if (!_parsers.TryGetValue(sectionType, out var type))
                    return null;

                return (ISectionParser)Activator.CreateInstance(type);
            }
        }
    }
}
