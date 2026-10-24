namespace Cthangover.Mods.Stats
{
    using System;
    using System.Linq;

    /// <summary>
    /// Current amounts of pool stats (<see cref="StatInfo.Pool"/>: mana, stamina, willpower).
    /// The sheet value is the maximum; the state keeps only what was spent, so a missing
    /// entry means full and a grown maximum is felt at once. Health is not a pool here: it
    /// already lives in the battle attributes, which every battle core reads and writes.
    /// </summary>
    public static class StatPools
    {
        /// <summary>The current amount of <paramref name="stat"/> (0..max).</summary>
        public static double Current(string who, string stat)
        {
            var id = StatsService.Resolve(who);
            var max = StatsService.Sheet(id).Get(stat);
            var state = StatState.Instance.Find(id);
            if (state == null || stat == null || !state.Pools.TryGetValue(stat.ToLowerInvariant(), out var current))
                return max;
            return Math.Clamp(current, 0, Math.Max(0, max));
        }

        /// <summary>Spends <paramref name="amount"/>; false (nothing spent) when there is not enough.</summary>
        public static bool Spend(string who, string stat, double amount)
        {
            var id = StatsService.Resolve(who);
            if (amount <= 0)
                return true;
            var current = Current(id, stat);
            if (current < amount)
                return false;
            StatState.Instance.Of(id).Pools[stat.ToLowerInvariant()] = current - amount;
            StatsService.Changed(id);
            return true;
        }

        /// <summary>Fills <paramref name="stat"/> (null: every pool) of <paramref name="who"/> to the maximum.</summary>
        public static void Refill(string who, string stat = null)
        {
            var id = StatsService.Resolve(who);
            var state = StatState.Instance.Find(id);
            if (state == null)
                return;
            if (stat == null)
                state.Pools.Clear();
            else
                state.Pools.Remove(stat.ToLowerInvariant());
            StatsService.Changed(id);
        }

        /// <summary>Whether <paramref name="stat"/> is defined as a pool.</summary>
        public static bool IsPool(string stat) => StatsService.Definitions.Any(d => d.Pool && string.Equals(d.ID, stat, StringComparison.OrdinalIgnoreCase));
    }
}
