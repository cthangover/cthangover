using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    public class StunStatusEffect : IStatusEffect
    {
        public int Type => 1;
        public int Turns { get; set; }
        public bool IsExpired => Turns <= 0;

        public StunStatusEffect(int turns)
        {
            Turns = turns;
        }

        public StunStatusEffect()
        {
        }

        public void OnTurnStart(Character character, IStatusActions actions)
        {
            actions.SkipTurn();
        }

        public void OnTurnEnd(Character character, IStatusActions actions)
        {
        }
    }
}
