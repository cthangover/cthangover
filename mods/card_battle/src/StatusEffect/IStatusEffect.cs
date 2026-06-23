using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    public interface IStatusEffect
    {
        int Type { get; }
        int Turns { get; set; }
        bool IsExpired { get; }

        void OnTurnStart(Character character, IStatusActions actions);
        void OnTurnEnd(Character character, IStatusActions actions);
    }
}
