using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// How a game's composition changed between a save and now: mods added
    /// (they start fresh), removed (their data goes dormant) and updated
    /// (migration runs; a downgrade keeps that mod's data dormant). Shown
    /// before loading so the player knows what happens to the save.
    /// </summary>
    public sealed class SaveCompositionDiff
    {
        public List<string> Added { get; } = new();

        public List<string> Removed { get; } = new();

        public List<(string ModId, string From, string To, bool Downgrade)> Updated { get; } = new();

        public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Updated.Count == 0;

        /// <summary>
        /// Compares the save's snapshot with the composition the game runs now.
        /// A save without a snapshot (written without a game) yields an empty
        /// diff — there is nothing reliable to compare.
        /// </summary>
        public static SaveCompositionDiff Compute(IEnumerable<SaveGameMod> saved, IEnumerable<SaveGameMod> current)
        {
            var diff = new SaveCompositionDiff();
            if (saved == null || current == null)
                return diff;

            var before = saved.Where(m => m?.Id != null).GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Version, StringComparer.OrdinalIgnoreCase);
            var now = current.Where(m => m?.Id != null).GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Version, StringComparer.OrdinalIgnoreCase);

            foreach (var pair in now.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!before.TryGetValue(pair.Key, out var was))
                {
                    diff.Added.Add(pair.Key);
                    continue;
                }

                if (!string.Equals(was, pair.Value, StringComparison.Ordinal) && !string.IsNullOrEmpty(was) && !string.IsNullOrEmpty(pair.Value))
                    diff.Updated.Add((pair.Key, was, pair.Value, Compare(was, pair.Value) > 0));
            }

            foreach (var id in before.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!now.ContainsKey(id))
                    diff.Removed.Add(id);
            }

            return diff;
        }

        private static int Compare(string a, string b)
        {
            try
            {
                return VersionHelper.Compare(a, b);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
