using System;
using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Bootstrap;
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
    /// <para/>
    /// The registry instance lives in <see cref="AppServices.Current"/>; the static
    /// members below are a facade delegating to it, so existing callers keep
    /// working unchanged.
    /// </summary>
    public class SectionParserRegistry : IGameRegistry
    {
        private readonly object Sync = new();
        private Dictionary<string, Type> _parsers;
        private bool built;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static SectionParserRegistry Owned => AppServices.Current.SectionParsers;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract. <see cref="Rebuild"/> fills the data.
        /// </summary>
        internal SectionParserRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "tscn_section_parsers";

        /// <summary>
        /// Scans all loaded assemblies for <see cref="ISectionParser"/>
        /// implementations unless already built. Thread-safe; keeps the old
        /// Initialize idempotence: later calls are no-ops until cleared.
        /// </summary>
        public void EnsureBuilt()
        {
            lock (Sync)
            {
                if (built)
                    return;

                RescanLocked();
                built = true;
            }
        }

        /// <summary>
        /// Forced full re-scan of all loaded assemblies (assembly order equals
        /// load order, so first-registered-wins keeps core parsers ahead of
        /// mod-added ones), replacing the previous table. Safe to call
        /// repeatedly; picks up assemblies loaded after the previous build.
        /// </summary>
        public void Rebuild()
        {
            lock (Sync)
            {
                RescanLocked();
                built = true;
            }
        }

        /// <summary>
        /// Drops all registered parser types; the next build re-scans the
        /// assemblies. Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        public void Clear()
        {
            lock (Sync)
            {
                _parsers = null;
                built = false;
            }
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>Legacy entry point; delegates to the shared instance.</summary>
        public static void Initialize() => Owned.EnsureBuilt();

        /// <summary>Scans a newly-loaded mod assembly for <see cref="ISectionParser"/> implementations and registers them.</summary>
        public static void RegisterAssembly(Assembly assembly)
            => Owned.AddFromAssembly(assembly);

        /// <summary>Creates a fresh parser instance for the given section type, or null when none is registered.</summary>
        public static ISectionParser Get(string sectionType)
            => Owned.GetParser(sectionType);

        // ---- Instance implementation --------------------------------------

        /// <summary>Full re-scan; caller must hold <see cref="Sync"/>.</summary>
        private void RescanLocked()
        {
            var parsers = new Dictionary<string, Type>();
            foreach (var parser in Reflections.FindAndCreate<ISectionParser>())
            {
                if (!parsers.ContainsKey(parser.SectionType))
                    parsers[parser.SectionType] = parser.GetType();
            }

            _parsers = parsers;
        }

        private void AddFromAssembly(Assembly assembly)
        {
            EnsureBuilt();

            lock (Sync)
            {
                foreach (var parser in Reflections.FindAndCreate<ISectionParser>(assembly))
                {
                    if (!_parsers.ContainsKey(parser.SectionType))
                        _parsers[parser.SectionType] = parser.GetType();
                }
            }
        }

        private ISectionParser GetParser(string sectionType)
        {
            EnsureBuilt();

            lock (Sync)
            {
                if (!_parsers.TryGetValue(sectionType, out var type))
                    return null;

                return (ISectionParser)Activator.CreateInstance(type);
            }
        }
    }
}
