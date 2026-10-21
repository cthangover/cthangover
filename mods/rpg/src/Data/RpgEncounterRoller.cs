using System;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// The random-battle roll made after each step on a map with an
    /// <see cref="RpgEncounterTable"/>. Takes the random source from outside so a
    /// test can make it deterministic.
    /// </summary>
    public static class RpgEncounterRoller
    {
        /// <summary>
        /// Returns the enemy group of the battle this step starts, or null for no
        /// battle (no table, encounters off, no groups, or the roll missed).
        /// </summary>
        public static string Roll(RpgEncounterTable table, Random random)
        {
            if (table == null || table.Steps <= 0 || table.Groups == null || table.Groups.Count == 0 || random == null)
                return null;
            if (random.Next(table.Steps) != 0)
                return null;

            var group = table.Groups[random.Next(table.Groups.Count)];
            return string.IsNullOrWhiteSpace(group) ? null : group;
        }
    }
}
