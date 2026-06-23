using System;

namespace Cthangover.Core.Characters
{
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
