namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Stats;

    /// <summary>Saves what everyone wears in the equipment slot.</summary>
    public class EquipmentSaveable : IModSaveable
    {
        private const string BlobKey = "Worn";

        /// <inheritdoc />
        public void OnSave(ModSlot slot)
        {
            slot.Data[BlobKey] = new DataBlob(1, EquipmentService.Data);
        }

        /// <inheritdoc />
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            EquipmentService.Data = new EquipmentStateData();
            if (slot?.Data != null && slot.Data.TryGetValue(BlobKey, out var blob))
                EquipmentService.Data = blob.ReadJson<EquipmentStateData>() ?? new EquipmentStateData();
            StatsService.ClearCache();
            return true;
        }
    }
}
