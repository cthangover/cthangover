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
        /// <inheritdoc cref="IStatusActions.ID"/>
        public string ID => "basic/stun";
        
        /// <inheritdoc cref="IStatusActions.OnTurnStart"/>
        public void OnTurnStart(Character target)
        { }

        /// <inheritdoc cref="IStatusActions.OnTurnEnd"/>
        public void OnTurnEnd(Character target)
        { }

        /// <inheritdoc cref="IStatusActions.OnDealDamage"/>
        public void OnDealDamage(Character source, Character target, ref int damage)
        { }

        /// <inheritdoc cref="IStatusActions.OnTakeDamage"/>
        public void OnTakeDamage(Character target, Character source, ref int damage)
        { }

        /// <inheritdoc cref="IStatusActions.OnApply"/>
        public void OnApply(Character target)
        { }

        /// <inheritdoc cref="IStatusActions.OnRemove"/>
        public void OnRemove(Character target)
        { }

        /// <inheritdoc cref="IStatusActions.OnFinalAction"/>
        public void OnFinalAction(Character target)
        { }

        /// <inheritdoc cref="IStatusActions.ModifyAttributes"/>
        public void ModifyAttributes(CharacterAttributes attributes)
        { }
    }
}
