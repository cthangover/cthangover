namespace Cthangover.Mods.Stats
{
    using System.Collections.Generic;

    /// <summary>
    /// Everything a stat sheet is computed from, gathered by <see cref="StatsService"/>
    /// from data (definitions, template, archetype), the saved state (bonuses, level) and
    /// the trait sources (equipment, statuses, …). Kept as a plain bag so
    /// <see cref="StatCalculator"/> stays a pure function that tests feed directly.
    /// </summary>
    public class StatInputs
    {
        /// <summary>The character's level (<c>level</c> in formulas).</summary>
        public int Level { get; set; } = 1;

        /// <summary>Stat definitions by id.</summary>
        public IReadOnlyDictionary<string, StatInfo> Definitions { get; set; } = new Dictionary<string, StatInfo>();

        /// <summary>The character's own <c>Stats</c> entries (value or formula per stat id).</summary>
        public IReadOnlyDictionary<string, string> Own { get; set; } = new Dictionary<string, string>();

        /// <summary>The character's archetype, or null.</summary>
        public ArchetypeInfo Archetype { get; set; }

        /// <summary>Permanent bonuses per stat id (potions, story choices) from the stats state.</summary>
        public IReadOnlyDictionary<string, double> Bonus { get; set; } = new Dictionary<string, double>();

        /// <summary>Every trait that applies to the character now, from all sources.</summary>
        public IReadOnlyList<TraitInfo> Traits { get; set; } = new List<TraitInfo>();
    }
}
