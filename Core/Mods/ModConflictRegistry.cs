using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Conflict registry between mods. ModManager records each resource
    /// override here. UI reads it to display to the player.
    /// <para/>
    /// Entries are filled lazily by ModManager during validation — there is no
    /// manifest source to re-read, so <see cref="Rebuild"/> only resets the
    /// table. The registry instance lives in <see cref="AppServices.Current"/>;
    /// the static members below are a facade delegating to it, so existing
    /// callers keep working unchanged.
    /// </summary>
    public class ModConflictRegistry : IGameRegistry
    {
        private readonly object _lock = new();
        private readonly List<ModConflictEntry> _conflicts = new();

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static ModConflictRegistry Owned => AppServices.Current.Conflicts;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract.
        /// </summary>
        internal ModConflictRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "mod_conflicts";

        /// <summary>
        /// Entries are recorded lazily by ModManager during validation — nothing
        /// to re-read from manifests, so rebuilding just resets the table.
        /// </summary>
        public void Rebuild()
        {
            Reset();
        }

        /// <summary>
        /// Drops all recorded conflicts; the next validation pass refills them.
        /// Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        void IGameRegistry.Clear()
        {
            Reset();
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>Snapshot of every recorded conflict (one entry per overridden resource).</summary>
        public static IReadOnlyList<ModConflictEntry> Conflicts => Owned.Snapshot();

        /// <summary>
        /// Records one resource override: <paramref name="winningModId"/>'s value
        /// became final and <paramref name="overriddenModId"/>'s was replaced.
        /// Array concatenations set <paramref name="wasConcatenated"/> so the UI
        /// can distinguish merged lists from full replacements.
        /// </summary>
        public static void Record(
            string resourceType,
            string resourceId,
            string winningModId,
            string overriddenModId,
            bool wasConcatenated,
            string note = null)
            => Owned.RecordConflict(resourceType, resourceId, winningModId, overriddenModId, wasConcatenated, note);

        /// <summary>Conflicts where the given mod's value won (it overrides others).</summary>
        public static IReadOnlyList<ModConflictEntry> GetOverridesBy(string modId)
            => Owned.SelectOverridesBy(modId);

        /// <summary>Conflicts where the given mod's value was overridden by another mod.</summary>
        public static IReadOnlyList<ModConflictEntry> GetOverridesOf(string modId)
            => Owned.SelectOverridesOf(modId);

        /// <summary>Drops all recorded conflicts; the next validation pass refills them.</summary>
        public static void Clear()
            => Owned.Reset();

        // ---- Instance implementation --------------------------------------

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void Reset()
        {
            lock (_lock)
                _conflicts.Clear();
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public IReadOnlyList<ModConflictEntry> Snapshot()
        {
            lock (_lock)
                return _conflicts.ToList();
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public void RecordConflict(
            string resourceType,
            string resourceId,
            string winningModId,
            string overriddenModId,
            bool wasConcatenated,
            string note = null)
        {
            lock (_lock)
            {
                _conflicts.Add(new ModConflictEntry
                {
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    WinningModId = winningModId,
                    OverriddenModId = overriddenModId,
                    WasConcatenated = wasConcatenated,
                    Note = note,
                });
            }
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public IReadOnlyList<ModConflictEntry> SelectOverridesBy(string modId)
        {
            lock (_lock)
                return _conflicts.Where(c => c.WinningModId == modId).ToList();
        }

        /// <summary>Direct instance access for core consumers migrating off the static facade.</summary>
        public IReadOnlyList<ModConflictEntry> SelectOverridesOf(string modId)
        {
            lock (_lock)
                return _conflicts.Where(c => c.OverriddenModId == modId).ToList();
        }
    }
}
