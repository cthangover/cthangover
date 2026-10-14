using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Characters
{
    /// <summary>
    /// Persists the character roster (party composition and per-character
    /// progression) through the generic <see cref="IModSaveable"/> contract
    /// under the mod id <c>characters_base</c>. The core save used to carry
    /// the <c>"Characters"</c> and <c>"BattleSet"</c> blobs inside the
    /// <c>__core__</c> slot (<c>RuntimeSerializer.BuildCoreSlot</c>); the
    /// roster belongs to the characters engine mod, so it moves to the mod
    /// slot like every other migrated subsystem (quests, items, recruiting).
    /// <para/>
    /// The state itself is not duplicated: <see cref="OnSave"/> marshals
    /// <c>GameData.Instance.Runtime.CharacterData</c> — the same runtime
    /// objects the core, battle and UI read — into the mod slot, and
    /// <see cref="OnLoad"/> writes the restored dictionaries back into that
    /// runtime, so no other code changes its data path. While the transition
    /// is staged (the core slot still carries the old blobs), load order is
    /// safe: the core restores <c>__core__</c> first and this saveable
    /// re-restores the identical data afterwards from the mod slot. When the
    /// core blobs are removed, old saves degrade gracefully —
    /// <c>RuntimeSerializer.RestoreCoreState</c> skips missing blob keys, and
    /// an absent mod blob keeps the fresh runtime roster.
    /// </summary>
    public class CharactersSaveable : IModSaveable
    {
        private const string CharactersBlobKey = "Characters";
        private const string BattleSetBlobKey = "BattleSet";

        /// <summary>Writes the roster and battle set into the mod slot.</summary>
        public void OnSave(ModSlot slot)
        {
            var characterData = GameData.Instance.Runtime.CharacterData;
            slot.Data[CharactersBlobKey] = new DataBlob(1, characterData.Characters.Values.ToList());
            slot.Data[BattleSetBlobKey] = new DataBlob(1, characterData.BattleSet.ToList());
        }

        /// <summary>
        /// Restores roster and battle set from the mod slot; an absent blob
        /// keeps the corresponding part of the current runtime state.
        /// </summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;

            if (slot?.Data == null)
                return true;

            var characterData = GameData.Instance.Runtime.CharacterData;

            if (slot.Data.TryGetValue(CharactersBlobKey, out var charactersBlob))
            {
                var characters = charactersBlob.ReadJson<List<CharacterInfoData>>();
                characterData.Characters = new Dictionary<string, CharacterInfoData>();
                if (characters != null)
                    foreach (var info in characters)
                        characterData.Characters[info.CharacterType] = info;
            }

            if (slot.Data.TryGetValue(BattleSetBlobKey, out var battleSetBlob))
                characterData.BattleSet = battleSetBlob.ReadJson<List<string>>()?.ToHashSet()
                    ?? new HashSet<string>();

            return true;
        }
    }
}
