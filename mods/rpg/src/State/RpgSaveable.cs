using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Saves the player's map position in the rpg mod's slot. The scene itself is
    /// saved by the core (the map is the current scene); loading enters that
    /// scene and the world reads the position back from <see cref="RpgState"/>.
    /// </summary>
    public class RpgSaveable : IModSaveable
    {
        private const string BlobKey = "Position";

        /// <summary>Writes the position.</summary>
        public void OnSave(ModSlot slot)
        {
            slot.Data[BlobKey] = new DataBlob(1, RpgState.Instance.Data);
        }

        /// <summary>Restores the position; a slot without it leaves the player at the spawn.</summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            RpgState.Instance.Reset();
            if (slot?.Data != null && slot.Data.TryGetValue(BlobKey, out var blob))
                RpgState.Instance.Data = blob.ReadJson<RpgStateData>() ?? new RpgStateData();
            return true;
        }
    }
}
