namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Actions;

    /// <summary>
    /// <c>action stat.restore [who=party|player|&lt;id&gt;] [stat=mp]</c>: an inn, a fountain, a
    /// good night's sleep. Without <c>stat</c> it fills health (living members only) and
    /// every pool; with <c>stat=health</c> or a pool id only that one. <c>who</c> defaults to
    /// the whole party.
    /// </summary>
    public class StatRestoreAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "stat.restore";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var stat = ctx.GetParam("stat");
            var who = ctx.GetParam("who");
            foreach (var id in StatsService.Targets(string.IsNullOrWhiteSpace(who) ? "party" : who))
            {
                var all = string.IsNullOrWhiteSpace(stat);
                if (all || stat.Trim() is "health" or "hp")
                    RestoreHealth(id);
                if (all)
                    StatPools.Refill(id);
                else if (StatPools.IsPool(stat))
                    StatPools.Refill(id, stat);
            }
        }

        private static void RestoreHealth(string id)
        {
            var health = StatsService.RosterEntry(id)?.Attributes?.Health;
            if (health == null || health.Value <= 0)
                return;
            health.Value = health.BaseValue;
            StatsService.Changed(id);
        }
    }
}
