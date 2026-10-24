namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;

    /// <summary>Saves what characters gained (<see cref="StatStateData"/>) in the stats slot.</summary>
    public class StatsSaveable : IModSaveable
    {
        private const string BlobKey = "Stats";

        /// <inheritdoc />
        public void OnSave(ModSlot slot)
        {
            slot.Data[BlobKey] = new DataBlob(1, StatState.Instance.Data);
        }

        /// <inheritdoc />
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            StatState.Instance.Reset();
            if (slot?.Data != null && slot.Data.TryGetValue(BlobKey, out var blob))
                StatState.Instance.Data = blob.ReadJson<StatStateData>() ?? new StatStateData();
            StatsService.ClearCache();
            return true;
        }
    }
}
