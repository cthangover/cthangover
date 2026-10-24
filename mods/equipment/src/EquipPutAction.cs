namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Actions;

    /// <summary>
    /// <c>action equip.put item=&lt;id&gt; [who=player|&lt;id&gt;] [slot=weapon] [give=1]</c>:
    /// puts an item on a character from a scenario — the starting gear of a class, a gift
    /// the hero wears at once. The item comes from the inventory; with <c>give=1</c> it is
    /// granted instead (it need not be in the bag).
    /// </summary>
    public class EquipPutAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "equip.put";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var item = ctx.GetParam("item");
            if (string.IsNullOrWhiteSpace(item))
            {
                ctx.LogWarning("EQUIPMENT", "equip.put: missing 'item'");
                return;
            }
            var slot = ctx.GetParam("slot");
            var give = ctx.GetParam("give");
            var fromInventory = string.IsNullOrWhiteSpace(give) || give.Trim() == "0";
            if (!EquipmentService.Equip(ctx.GetParam("who"), item.Trim(), string.IsNullOrWhiteSpace(slot) ? null : slot.Trim(), fromInventory))
                ctx.LogWarning("EQUIPMENT", $"equip.put: '{item}' was not put on (not gear, no fitting slot, not allowed or not in the inventory)");
        }
    }
}
