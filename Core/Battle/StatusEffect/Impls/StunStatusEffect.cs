using Cthangover.Core.Characters;

namespace Cthangover.Core.Cards.StatusEffect.Impls
{
    public class StunStatusEffect : IStatusActions
    {
        public string ID => "basic/stun";
        
        public void OnTurnStart(Character target)
        { }

        public void OnTurnEnd(Character target)
        { }

        public void OnDealDamage(Character source, Character target, ref int damage)
        { }

        public void OnTakeDamage(Character target, Character source, ref int damage)
        { }

        public void OnApply(Character target)
        { }

        public void OnRemove(Character target)
        { }

        public void OnFinalAction(Character target)
        { }

        public void ModifyAttributes(CharacterAttributes attributes)
        { }
    }
}
