using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Conflict registry between mods. ModManager records each resource
    /// override here. UI reads it to display to the player.
    /// </summary>
    public static class ModConflictRegistry
    {
        private static readonly object _lock = new();
        private static readonly List<ModConflictEntry> _conflicts = new();

        public static IReadOnlyList<ModConflictEntry> Conflicts
        {
            get
            {
                lock (_lock)
                    return _conflicts.ToList();
            }
        }

        public static void Record(
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

        public static IReadOnlyList<ModConflictEntry> GetOverridesBy(string modId)
        {
            lock (_lock)
                return _conflicts.Where(c => c.WinningModId == modId).ToList();
        }

        public static IReadOnlyList<ModConflictEntry> GetOverridesOf(string modId)
        {
            lock (_lock)
                return _conflicts.Where(c => c.OverriddenModId == modId).ToList();
        }

        public static void Clear()
        {
            lock (_lock)
                _conflicts.Clear();
        }
    }
}
