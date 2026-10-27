namespace Cthangover.Mods.Crafting
{
    using Cthangover.Core.Actions;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.UI;

    /// <summary>Opens a data-defined station using the shared overlay host, including its walking/input blocker.</summary>
    public class CraftOpenAction : IScenarioAction
    {
        /// <summary>Scenario action id.</summary>
        public string Name => "craft.open";

        /// <summary>Resolves the station from the current entity snapshot and opens its window.</summary>
        public void Run(IActionContext ctx)
        {
            var id = ctx.GetParam("station")?.Trim();
            var stations = Entities.Collect<CraftStation>();
            if (id == null || !stations.TryGetValue(id, out var station))
            {
                ctx.LogWarning("CRAFT", $"craft.open: unknown station '{id}'");
                return;
            }
            var entry = UiRegistry.Get(CraftPanel.EntryId);
            if (entry == null || UiHost.Instance?.EnsureInstance(entry) is not CraftPanel panel)
            {
                ctx.LogWarning("CRAFT", "craft.open: crafting overlay is unavailable");
                return;
            }
            panel.SetStation(station);
            if (!panel.Visible)
                UiRegistry.Toggle(CraftPanel.EntryId);
        }
    }
}
