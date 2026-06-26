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
        public int Discipline { get; set; } = -5;
        public int Depravity { get; set; } = -5;
        public int Fullness { get; set; } = 100;

        public Attribute Health { get; set; } = new Attribute();
        public Attribute Defence { get; set; } = new Attribute();
        public Attribute Attack { get; set; } = new Attribute();
        public Attribute Strength { get; set; } = new Attribute();
        public Attribute Magic { get; set; } = new Attribute();
        public Attribute Point { get; set; } = new Attribute();

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
