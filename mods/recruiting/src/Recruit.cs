using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Runtime model for a recruited party member. Stores the recruit's
    /// unique ID, the character template it was created from, and a
    /// modifiable <see cref="Utility.PropertyData"/> bag that behaviours
    /// read and mutate during tick/configure events.
    ///
    /// Marked <c>[Serializable]</c> so the recruiting roster survives
    /// save/load — <c>PropertyData</c> serialises its nested values
    /// automatically. The <c>PROP_HEALTH</c> constant is the conventional
    /// key for health tracking; behaviours that need custom state
    /// (e.g. morale, loyalty timers) add their own keys to the
    /// <c>Properties</c> bag at configure time.
    /// </summary>
    [Serializable]
    public class Recruit
    {
        /// <summary>Conventional key for the recruit's health value in the <c>Properties</c> bag.</summary>
        public const string PROP_HEALTH = "Health";

        /// <summary>Unique instance ID (auto-generated via <c>RecruitingData.GenID</c>).</summary>
        public string       ID          { get; set; }

        /// <summary>Character template ID this recruit was spawned from.</summary>
        public string       CharacterID { get; set; }

        /// <summary>Mutable property bag — behaviours read/write custom state here.</summary>
        public PropertyData Properties  { get; set; } = new();
    }

}
