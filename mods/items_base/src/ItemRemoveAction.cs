namespace Cthangover.Mods.Items
{
    using Cthangover.Core.Actions;

    /// <summary>Consumes a positive stack from a scenario. Gate deliveries with item('id') >= count before running it.</summary>
    public class ItemRemoveAction : IScenarioAction
    {
        /// <summary>Scenario id.</summary>
        public string Name => "item.remove";

        /// <summary>Refuses an invalid or insufficient stack without changing inventory.</summary>
        public void Run(IActionContext ctx)
        {
            var id = ctx.GetParam("id")?.Trim();
            var text = ctx.GetParam("count");
            var count = 1;
            if (string.IsNullOrWhiteSpace(id) || (!string.IsNullOrWhiteSpace(text) && !int.TryParse(text, out count)) || count <= 0)
            {
                ctx.LogWarning("INVENTORY", "item.remove: expected an item id and a positive count");
                return;
            }
            if (ctx.Inventory?.Remove(id, count) != true)
                ctx.LogWarning("INVENTORY", $"item.remove: insufficient '{id}' (need {count})");
        }
    }
}
