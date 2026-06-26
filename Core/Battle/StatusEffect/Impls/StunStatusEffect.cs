using Cthangover.Core.Characters;

namespace Cthangover.Core.Cards.StatusEffect.Impls
{
    /// <summary>
    /// Stun behaviour set — intentionally all no-ops. The stun condition
    /// is checked separately via StatusEffectQueue.HasStun() rather than
    /// through action hooks, so the Actions implementation is empty.
    /// Exists as a concrete type so the factory can resolve "basic/stun"
    /// to a valid instance.
    /// </summary>
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
