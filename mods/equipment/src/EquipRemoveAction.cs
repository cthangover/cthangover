namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Actions;

    /// <summary><c>action equip.remove slot=weapon [who=player|&lt;id&gt;]</c>: takes off what is worn there, back into the inventory.</summary>
    public class EquipRemoveAction : IScenarioAction
    {
        /// <inheritdoc />
        public string Name => "equip.remove";

        /// <inheritdoc />
        public void Run(IActionContext ctx)
        {
            var slot = ctx.GetParam("slot");
            if (string.IsNullOrWhiteSpace(slot))
            {
                ctx.LogWarning("EQUIPMENT", "equip.remove: missing 'slot'");
                return;
            }
            EquipmentService.Unequip(ctx.GetParam("who"), slot.Trim());
        }
    }
}
