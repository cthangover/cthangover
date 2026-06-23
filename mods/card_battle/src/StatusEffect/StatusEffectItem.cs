namespace Cthangover.CardBattle.StatusEffect
{
    public class StatusEffectItem
    {
        public int Type { get; set; }
        public int Turns { get; set; }

        public StatusEffectItem Copy()
        {
            return new StatusEffectItem
            {
                Type = Type,
                Turns = Turns
            };
        }
    }
}
