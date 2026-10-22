using Cthangover.Core.Actions;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// <c>action item.add id=resource/branches [count=3]</c> — puts items into the
    /// player's inventory from a scenario: a chest, a gift, a pickup on an RPG map.
    /// <c>count</c> defaults to 1; an unknown item id is reported by the inventory
    /// and adds nothing.
    /// </summary>
    public class ItemAddAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "item.add";

        /// <summary>Reads <c>id</c> and <c>count</c> and adds the stack.</summary>
        public void Run(IActionContext ctx)
        {
            var id = ctx.GetParam("id");
            if (string.IsNullOrWhiteSpace(id))
            {
                ctx.LogWarning("INVENTORY", "item.add: missing 'id'");
                return;
            }

            var countText = ctx.GetParam("count");
            var count = 1;
            if (!string.IsNullOrWhiteSpace(countText) && (!int.TryParse(countText, out count) || count <= 0))
            {
                ctx.LogWarning("INVENTORY", $"item.add: 'count' must be a positive number, got '{countText}'");
                return;
            }

            if (ctx.Inventory == null)
            {
                ctx.LogWarning("INVENTORY", "item.add: no inventory in this game");
                return;
            }
            ctx.Inventory.Add(id.Trim(), count);
        }
    }
}
