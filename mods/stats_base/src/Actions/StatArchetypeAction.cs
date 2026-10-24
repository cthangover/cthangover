namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Actions;

    /// <summary>
    /// <c>action stat.archetype id=warrior [who=player|&lt;id&gt;]</c>: gives a character an
    /// archetype (a class, a temperament). A prologue's "who are you?" choice is three
    /// options each ending with this action. An empty id returns to the template's.
    /// </summary>
    public class StatArchetypeAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "stat.archetype";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var archetype = ctx.GetParam("id");
            if (!string.IsNullOrWhiteSpace(archetype) && StatsService.Archetype(archetype) == null)
                ctx.LogWarning("STATS", $"stat.archetype: unknown archetype '{archetype}'");
            foreach (var id in StatsService.Targets(ctx.GetParam("who")))
                StatsService.SetArchetype(id, archetype);
        }
    }
}
