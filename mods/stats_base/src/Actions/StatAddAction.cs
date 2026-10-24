namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Actions;

    /// <summary>
    /// <c>action stat.add stat=cha value=1 [who=player|party|&lt;id&gt;]</c>: a permanent change
    /// of a stat — a training session, a potion of strength, a choice that made the hero
    /// braver. Negative values lower the stat.
    /// </summary>
    public class StatAddAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "stat.add";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var stat = ctx.GetParam("stat");
            var value = StatArgs.Number(ctx.GetParam("value"), 1);
            if (string.IsNullOrWhiteSpace(stat))
            {
                ctx.LogWarning("STATS", "stat.add: missing 'stat'");
                return;
            }
            foreach (var id in StatsService.Targets(ctx.GetParam("who")))
                StatsService.AddBonus(id, stat, value);
        }
    }
}
