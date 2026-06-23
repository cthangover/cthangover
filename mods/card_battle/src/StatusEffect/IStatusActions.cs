namespace Cthangover.CardBattle.StatusEffect
{
    public interface IStatusActions
    {
        void SkipTurn();
        void RemoveStatus(IStatusEffect status);
    }
}
