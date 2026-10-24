namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The registered <see cref="ITraitSource"/>s. Filled by <see cref="TraitSourceConsumer"/>
    /// per mod assembly, so a source leaves with its mod on reload; a throwing source is
    /// logged and skipped, it never breaks the sheet of everyone else.
    /// </summary>
    public static class TraitSources
    {
        private static readonly List<ITraitSource> Sources = new();

        /// <summary>The registered sources, in registration order.</summary>
        public static IReadOnlyList<ITraitSource> All => Sources;

        /// <summary>
        /// Adds a source (ignored when already present). Only the cached sheets are dropped:
        /// sources arrive while mods register, before any game state exists, so this must
        /// not reach the roster (the next scene entry syncs it).
        /// </summary>
        public static void Add(ITraitSource source)
        {
            if (source != null && !Sources.Contains(source))
                Sources.Add(source);
            StatsService.ClearCache();
        }

        /// <summary>Removes a source (cached sheets are dropped, see <see cref="Add"/>).</summary>
        public static void Remove(ITraitSource source)
        {
            if (Sources.Remove(source))
                StatsService.ClearCache();
        }

        /// <summary>Every trait all sources give <paramref name="characterId"/>.</summary>
        public static List<TraitInfo> Collect(string characterId)
        {
            var result = new List<TraitInfo>();
            foreach (var source in Sources.ToList())
            {
                try
                {
                    var traits = source.TraitsOf(characterId);
                    if (traits != null)
                        result.AddRange(traits.Where(t => t != null));
                }
                catch (Exception ex)
                {
                    GameLogger.Log("STATS", $"trait source '{source.Id}' failed for '{characterId}': {ex.Message}", LogLevel.Error);
                }
            }
            return result;
        }
    }
}
