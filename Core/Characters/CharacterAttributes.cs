using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// The stat block for a character. Health, Defence, Attack, and Point
    /// use the <see cref="Attribute"/> wrapper (current + base + change events)
    /// because they are the core battle stats accessed by C# battle systems.
    /// Any mod-defined extras — live in the string-keyed
    /// <see cref="Properties"/> bag so that mods can add arbitrary stats
    /// without schema changes.
    /// </summary>
    [Serializable]
    public class CharacterAttributes
    {
        /// <summary>
        /// Health points wrapped in <see cref="Attribute"/> for event-
        /// driven change tracking (.Value = current, .BaseValue = max).
        /// </summary>
        public Attribute Health { get; set; } = new();
        /// <summary>
        /// Defence stat wrapped in <see cref="Attribute"/>. Reduces
        /// incoming damage.
        /// </summary>
        public Attribute Defence { get; set; } = new();
        /// <summary>
        /// Attack power wrapped in <see cref="Attribute"/>. Base damage
        /// dealt by this character.
        /// </summary>
        public Attribute Attack { get; set; } = new();
        /// <summary>
        /// Action points wrapped in <see cref="Attribute"/>. Resource
        /// pool consumed by abilities during battle.
        /// </summary>
        public Attribute Point { get; set; } = new();

        /// <summary>
        /// String-keyed property bag for all non-core stats: any mod-defined
        /// extras. Keys are free-form strings — mods define their own
        /// without registry.
        /// </summary>
        public PropertyData Properties { get; set; } = new();

        /// <summary>
        /// Creates an independent deep copy. All four <see cref="Attribute"/>
        /// fields are cloned via <see cref="Attribute.Copy"/> (preserving
        /// Value/BaseValue but dropping OnChange subscribers).
        /// <see cref="Properties"/> is cloned via <see cref="PropertyData.Clone"/>
        /// which duplicates the key-value dictionary. Interceptor chains
        /// are NOT copied — they are runtime behaviour, not data.
        /// </summary>
        public CharacterAttributes Copy()
        {
            return new CharacterAttributes()
            {
                Health = Health.Copy(),
                Defence = Defence.Copy(),
                Attack = Attack.Copy(),
                Point = Point.Copy(),
                Properties = Properties.Clone(),
            };
        }
    }
}
