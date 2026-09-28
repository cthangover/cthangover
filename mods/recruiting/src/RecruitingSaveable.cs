using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Persists the recruit roster in the mod's own save slot. The core save used
    /// to carry a <c>"Recruits"</c> blob inside the <c>__core__</c> slot, but the
    /// roster now belongs to the recruiting mod — so it saves through the generic
    /// <see cref="IModSaveable"/> contract under the mod id <c>recruiting</c>.
    /// <para/>
    /// <see cref="OnLoad"/> replaces the roster wholesale and does not re-fire
    /// <c>ConfigureRecruit</c>: persisted per-recruit properties (for example the
    /// nutrition timer) are already in the blob and behaviours continue their
    /// tick logic from the restored state, matching the old core behaviour.
    /// Backward compatibility with the old core blob is intentionally not
    /// provided — old saves start with an empty roster.
    /// </summary>
    public class RecruitingSaveable : IModSaveable
    {
        private const string BlobKey = "Recruits";

        /// <summary>Writes the session roster into the mod slot.</summary>
        public void OnSave(ModSlot slot)
        {
            slot.Data[BlobKey] = new DataBlob(1, RecruitingData.Instance.Data);
        }

        /// <summary>Restores the roster from the mod slot; an absent blob yields an empty roster.</summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;

            var data = new List<Recruit>();
            if (slot?.Data != null && slot.Data.TryGetValue(BlobKey, out var blob))
                data = blob.ReadJson<List<Recruit>>() ?? new List<Recruit>();

            RecruitingData.Instance.Data = data;
            return true;
        }
    }
}
