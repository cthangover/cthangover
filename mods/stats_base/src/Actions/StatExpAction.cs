namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Actions;

    /// <summary>
    /// <c>action stat.exp value=30 [who=party|player|&lt;id&gt;]</c>: exp outside battles — a
    /// quest reward, a lesson learned; level-ups follow at once. <c>who</c> defaults to the
    /// whole party.
    /// </summary>
    public class StatExpAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "stat.exp";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var value = (int)StatArgs.Number(ctx.GetParam("value"));
            if (value <= 0)
            {
                ctx.LogWarning("STATS", "stat.exp: 'value' must be a positive number");
                return;
            }
            var who = ctx.GetParam("who");
            foreach (var id in StatsService.Targets(string.IsNullOrWhiteSpace(who) ? "party" : who))
                StatProgression.AddExp(id, value);
        }
    }
}
