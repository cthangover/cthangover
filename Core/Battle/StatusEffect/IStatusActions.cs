using Cthangover.Core.Characters;

namespace Cthangover.Core.Cards.StatusEffect
{
    public interface IStatusActions
    {
        string ID { get; }
        
        void OnTurnStart(Character target);
        void OnTurnEnd(Character target);
        void OnDealDamage(Character source, Character target, ref int damage);
        void OnTakeDamage(Character target, Character source, ref int damage);
        void OnApply(Character target);
        void OnRemove(Character target);
        void OnFinalAction(Character target);
        void ModifyAttributes(CharacterAttributes attributes);
    }
}
