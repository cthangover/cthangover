using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Container and factory for the party's recruit roster. The roster is owned
    /// by the recruiting mod through the static <see cref="Instance"/> instead of
    /// living in the core <c>RuntimeData</c>, so the core knows nothing about
    /// recruiting. Persistence is handled by the mod's <c>RecruitingSaveable</c>
    /// (<c>IModSaveable</c>), and a new game clears the roster through the
    /// <c>NewGameStartedEvent</c> subscription in <c>RecruitingInitializer</c>.
    /// <para/>
    /// Provides ID-based lookup, duplicate-safe addition (auto-generates a unique
    /// <c>Guid</c> if the requested ID collides, and rejects a second recruit of
    /// the same enemy type), and removal that notifies
    /// <see cref="RecruitBehaviourRegistry"/> so behaviours can clean up and
    /// detaches the recruit from the squad through <see cref="RecruitPartyBridge"/>.
    /// <see cref="Add"/> initialises the recruit's <c>Properties</c> with a default
    /// health of 10 via <see cref="Recruit.PROP_HEALTH"/> and immediately calls
    /// <c>RecruitBehaviourRegistry.OnConfigure</c>, giving every registered
    /// behaviour a chance to set up per-recruit state before the next tick fires.
    /// </summary>
    public class RecruitingData
    {
        /// <summary>
        /// Session-wide roster. There is exactly one roster per playthrough —
        /// starting a new game clears it, loading a save replaces its contents.
        /// </summary>
        public static readonly RecruitingData Instance = new();

        /// <summary>The current recruit roster.</summary>
        public List<Recruit> Data { get; set; } = new();

        /// <summary>True if a recruit with the given ID exists in the roster.</summary>
        public bool HasID(string id)
        {
            return GetByID(id) != null;
        }

        /// <summary>Returns the recruit with the given ID, or null if not found.</summary>
        public Recruit GetByID(string id)
        {
            foreach (var item in Data)
            {
                if (item.ID == id)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// True if a recruit spawned from the given character template is already
        /// in the roster. Recruitment allows only one instance per enemy type,
        /// which also keeps the template id usable as a unique party key.
        /// </summary>
        public bool HasCharacterID(string characterID)
        {
            return GetByCharacterID(characterID) != null;
        }

        /// <summary>Returns the recruit spawned from the given template, or null if not found.</summary>
        public Recruit GetByCharacterID(string characterID)
        {
            foreach (var item in Data)
            {
                if (item.CharacterID == characterID)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Generates a unique ID: if <paramref name="id"/> is free,
        /// returns it unchanged; otherwise appends a <c>Guid</c> suffix.
        /// Loops until an unused ID is found (guaranteed because Guid
        /// is unique enough for any practical roster size).
        /// </summary>
        private string GenID(string id)
        {
            for (;;)
            {
                if (!HasID(id))
                    return id;
                id = Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Recruit"/> from the given
        /// <paramref name="characterID"/>, assigns a unique instance ID,
        /// initialises default health, appends to the roster, and fires
        /// <c>RecruitBehaviourRegistry.OnConfigure</c> so behaviours
        /// can set up per-recruit state.
        /// <para/>
        /// Only one recruit per enemy type is allowed: a second attempt for an
        /// already captured <paramref name="characterID"/> is ignored and the
        /// existing entry is returned, so callers cannot create duplicates and
        /// the template id remains a unique party card key.
        /// </summary>
        public Recruit Add(string id, string characterID)
        {
            var existing = GetByCharacterID(characterID);
            if (existing != null)
            {
                GameLogger.Log("RECRUIT",
                    $"Recruit '{characterID}' already captured, duplicate ignored", LogLevel.Warning);
                return existing;
            }

            var data = new Recruit
            {
                ID = GenID(id),
                CharacterID = characterID,
            };
            data.Properties.SetInt(Recruit.PROP_HEALTH, 10);
            Data.Add(data);
            
            RecruitBehaviourRegistry.Instance.OnConfigure(data);
            return data;
        }

        /// <summary>
        /// Removes the recruit with the given ID from the roster.
        /// Notifies <see cref="RecruitBehaviourRegistry.OnRemove"/>
        /// before deleting the entry so behaviours can persist state
        /// or clean up companion nodes, then detaches the recruit from the
        /// party and deletes its placeholder card via
        /// <see cref="RecruitPartyBridge.Forget"/>, so no orphan party member
        /// survives in the core save.
        /// </summary>
        public void Remove(string id)
        {
            for (int i = Data.Count - 1; i >= 0; i--)
            {
                if (Data[i].ID == id)
                {
                    RecruitBehaviourRegistry.Instance.OnRemove(Data[i]);
                    RecruitPartyBridge.Forget(Data[i]);
                    Data.RemoveAt(i);
                    return;
                }
            }
        }
        
    }

}
