using System;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// The complete stat block for a character. Health/Defence/Attack/Strength/
    /// Magic/Point use the Attribute wrapper (current + base + change events).
    /// Discipline, Depravity, and Fullness are raw ints with no events — they
    /// represent persistent character traits that change less frequently and
    /// don't need real-time UI binding. Initial values (-5, -5, 100) suggest
    /// Discipline/Depravity use a bipolar scale centered at 0, while Fullness
    /// starts full and depletes. Copy() deep-clones all Attributes to avoid
    /// shared mutable state.
    /// </summary>
    [Serializable]
    public class CharacterAttributes
    {
        /// <summary>
        /// Discipline trait: persistent character trait on a bipolar
        /// scale centered at 0. Positive = disciplined, negative =
        /// undisciplined. Defaults to -5. Stored as a raw int (no event
        /// tracking) because it changes infrequently and doesn't need
        /// real-time UI bindings.
        /// </summary>
        public int Discipline { get; set; } = -5;
        /// <summary>
        /// Depravity trait: persistent character trait on a bipolar scale
        /// centered at 0. Negative values represent more depravity.
        /// Defaults to -5. Like <see cref="Discipline"/>, a raw int with
        /// no change events.
        /// </summary>
        public int Depravity { get; set; } = -5;
        /// <summary>
        /// Fullness (satiety/hunger): starts at 100 (full) and depletes
        /// over time or through actions. Raw int — no events.
        /// </summary>
        public int Fullness { get; set; } = 100;

        /// <summary>
        /// Health points wrapped in <see cref="Attribute"/> for event-
        /// driven change tracking (.Value = current, .BaseValue = max).
        /// </summary>
        public Attribute Health { get; set; } = new Attribute();
        /// <summary>
        /// Defence stat wrapped in <see cref="Attribute"/>. Reduces
        /// incoming damage.
        /// </summary>
        public Attribute Defence { get; set; } = new Attribute();
        /// <summary>
        /// Attack power wrapped in <see cref="Attribute"/>. Base damage
        /// dealt by this character.
        /// </summary>
        public Attribute Attack { get; set; } = new Attribute();
        /// <summary>
        /// Strength modifier wrapped in <see cref="Attribute"/>. Scales
        /// physical damage.
        /// </summary>
        public Attribute Strength { get; set; } = new Attribute();
        /// <summary>
        /// Magic modifier wrapped in <see cref="Attribute"/>. Scales
        /// magical damage and healing.
        /// </summary>
        public Attribute Magic { get; set; } = new Attribute();
        /// <summary>
        /// Action points wrapped in <see cref="Attribute"/>. Resource
        /// pool consumed by abilities during battle.
        /// </summary>
        public Attribute Point { get; set; } = new Attribute();

        /// <summary>
        /// Deep-clones all <see cref="Attribute"/> instances via their
        /// <see cref="Attribute.Copy"/> methods. Raw int properties
        /// (Discipline, Depravity, Fullness) are value-copied. This is
        /// used by <see cref="Character.Copy"/> to isolate battle
        /// instances from template data.
        /// </summary>
        public CharacterAttributes Copy()
        {
            return new CharacterAttributes()
            {
                Health = Health.Copy(),
                Defence = Defence.Copy(),
                Attack = Attack.Copy(),
                Strength = Strength.Copy(),
                Magic = Magic.Copy(),
                Point = Point.Copy(),

                Discipline = Discipline,
                Depravity = Depravity,
                Fullness = Fullness,
            };
        }
    }
}
