using Cthangover.Core.Characters;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Bridge between the mod-owned recruit roster and the core party model.
    /// Core knows nothing about recruiting, so the mod materializes a placeholder
    /// <see cref="CharacterInfoData"/> for every recruit the player puts into the
    /// squad: the entry is keyed by the template <see cref="Recruit.CharacterID"/>
    /// (the roster guarantees at most one recruit per enemy, so the template id
    /// stays a unique party key) and therefore needs no core changes — the
    /// character panel, battle, item targeting and the save system all pick the
    /// recruit up as a normal party member.
    /// <para/>
    /// The recruit itself remains the source of truth outside battle: the mod
    /// calls <see cref="SyncToCard"/> whenever its own data changes (starvation
    /// tick, joining the squad) and <see cref="SyncFromCard"/> once battle is
    /// over, where the battle systems have already written the final health back
    /// through the shared <see cref="CharacterInfoData.Attributes"/> reference.
    /// <see cref="SyncToCard"/> must mutate the attribute in place — reassigning
    /// the attributes object would detach it from the battle copies that still
    /// point at the original instance.
    /// </summary>
    public static class RecruitPartyBridge
    {
        /// <summary>True when the recruit currently occupies a slot in the active party.</summary>
        public static bool IsInSquad(Recruit recruit)
        {
            var characterData = GetCharacterData();
            return characterData != null
                && recruit != null
                && characterData.BattleSet.Contains(recruit.CharacterID);
        }

        /// <summary>
        /// Adds the recruit to the active party. On the first join a placeholder
        /// card is created from the character template (attributes, portrait and
        /// default action slots); the card is then immediately synchronized with
        /// the recruit's own health. Repeated calls are idempotent.
        /// </summary>
        public static void AddToSquad(Recruit recruit)
        {
            var characterData = GetCharacterData();
            if (characterData == null || recruit == null)
                return;

            if (!characterData.Characters.ContainsKey(recruit.CharacterID))
                characterData.AddCharacterToParty(recruit.CharacterID);

            characterData.BattleSet.Add(recruit.CharacterID);
            SyncToCard(recruit);

            GameLogger.Log("RECRUIT", $"Recruit '{recruit.CharacterID}' joined the squad", LogLevel.Message);
        }

        /// <summary>
        /// Removes the recruit from the active party but keeps the placeholder
        /// card in <see cref="CharacterData.Characters"/> — the player's action
        /// assignments and battle-earned experience survive a temporary leave and
        /// are restored when the recruit joins again.
        /// </summary>
        public static void RemoveFromSquad(Recruit recruit)
        {
            var characterData = GetCharacterData();
            if (characterData == null || recruit == null)
                return;

            if (characterData.BattleSet.Remove(recruit.CharacterID))
                GameLogger.Log("RECRUIT", $"Recruit '{recruit.CharacterID}' left the squad", LogLevel.Message);
        }

        /// <summary>
        /// Drops every trace of the recruit: leaves the party and deletes the
        /// placeholder card. Called when the recruit is removed from the roster
        /// for good, so no orphan party entry survives in the core save.
        /// </summary>
        public static void Forget(Recruit recruit)
        {
            var characterData = GetCharacterData();
            if (characterData == null || recruit == null)
                return;

            RemoveFromSquad(recruit);
            characterData.Characters.Remove(recruit.CharacterID);
        }

        /// <summary>
        /// Pushes the recruit's health from its property bag into the placeholder
        /// card. The attribute is mutated in place: battle copies share the same
        /// <see cref="CharacterAttributes"/> instance, so a reassignment here
        /// would leave the fight running on the stale object.
        /// </summary>
        public static void SyncToCard(Recruit recruit)
        {
            var info = GetCard(recruit);
            if (info == null)
                return;

            info.Attributes ??= new CharacterAttributes();
            info.Attributes.Health.Value = recruit.Properties.GetInt(Recruit.PROP_HEALTH);
        }

        /// <summary>
        /// Pulls the health written by battle back into the recruit's property
        /// bag. Called on the core-neutral <c>characters_defeated</c> event, which
        /// the battle mod publishes after the victory screen resolves, so the
        /// mod's own data stays authoritative once the fight is over.
        /// </summary>
        public static void SyncFromCard(Recruit recruit)
        {
            var info = GetCard(recruit);
            if (info?.Attributes?.Health == null)
                return;

            recruit.Properties.SetInt(Recruit.PROP_HEALTH, info.Attributes.Health.Value);
        }

        private static CharacterInfoData GetCard(Recruit recruit)
        {
            var characterData = GetCharacterData();
            if (characterData?.Characters == null || recruit == null)
                return null;

            return characterData.Characters.TryGetValue(recruit.CharacterID, out var info) ? info : null;
        }

        private static CharacterData GetCharacterData()
        {
            return GameData.Instance?.Runtime?.CharacterData;
        }
    }
}
