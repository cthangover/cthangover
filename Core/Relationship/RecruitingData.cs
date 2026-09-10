using System;
using System.Collections.Generic;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Container and factory for the party's recruit roster, stored as
    /// part of <see cref="Settings.RuntimeData"/>. Provides ID-based
    /// lookup, duplicate-safe addition (auto-generates a unique <c>Guid</c>
    /// if the requested ID collides), and removal that notifies
    /// <see cref="RecruitBehaviourRegistry"/> so behaviours can clean up.
    ///
    /// The <c>Add</c> method initialises the recruit's <c>Properties</c>
    /// with a default health of 10 via <c>Recruit.PROP_HEALTH</c> and
    /// immediately calls <c>RecruitBehaviourRegistry.OnConfigure</c>,
    /// giving every registered behaviour a chance to set up per-recruit
    /// state before the next tick fires.
    /// </summary>
    public class RecruitingData
    {
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
        /// </summary>
        public Recruit Add(string id, string characterID)
        {
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
        /// or clean up companion nodes.
        /// </summary>
        public void Remove(string id)
        {
            for (int i = Data.Count - 1; i >= 0; i--)
            {
                if (Data[i].ID == id)
                {
                    RecruitBehaviourRegistry.Instance.OnRemove(Data[i]);
                    Data.RemoveAt(i);
                    return;
                }
            }
        }
        
    }

}
