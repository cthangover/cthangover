namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Bootstrap;

    /// <summary>A new game (or the reset before a load) starts with nobody wearing anything.</summary>
    public class EquipmentReset : IResettable
    {
        /// <inheritdoc />
        public string Id => "equipment";

        /// <inheritdoc />
        public void ResetForNewGame() => EquipmentService.Reset();
    }
}
