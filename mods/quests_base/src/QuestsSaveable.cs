using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Persists quest state through the generic <see cref="IModSaveable"/>
    /// contract under the mod id <c>quests_base</c>. The core save used to
    /// carry a <c>"Quests"</c> blob inside the <c>__core__</c> slot
    /// (<c>RuntimeSerializer.BuildCoreSlot</c>), but quest state now belongs to
    /// the quests engine mod — so it saves through the mod slot like every
    /// other migrated subsystem.
    /// <para/>
    /// <see cref="OnLoad"/> feeds the deserialised list into
    /// <see cref="QuestFactory.SetAll"/>, which restores mutable state per
    /// quest ID, skips quests that are no longer registered and publishes the
    /// single <c>QuestStateRestoredEvent</c> invalidation marker — exactly the
    /// behaviour the core save path had. Backward compatibility with the old
    /// core blob is intentionally not provided (same decision as the
    /// recruiting mod): old saves start with fresh quest state.
    /// </summary>
    public class QuestsSaveable : IModSaveable
    {
        private const string BlobKey = "Quests";

        /// <summary>Writes the full quest state list into the mod slot.</summary>
        public void OnSave(ModSlot slot)
        {
            slot.Data[BlobKey] = new DataBlob(1, QuestFactory.Instance.GetAll());
        }

        /// <summary>Restores quest state from the mod slot; an absent blob keeps fresh quest state.</summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;

            if (slot?.Data != null && slot.Data.TryGetValue(BlobKey, out var blob))
                QuestFactory.Instance.SetAll(blob.ReadJson<List<QuestBase>>());

            return true;
        }
    }
}
