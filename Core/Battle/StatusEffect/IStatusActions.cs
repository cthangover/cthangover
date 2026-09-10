using Cthangover.Core.Characters;

namespace Cthangover.Core.Cards.StatusEffect
{
    /// <summary>
    /// Behaviour hooks for a status effect type. Each hook receives the
    /// owning character so a single IStatusActions instance can be
    /// shared across multiple characters. OnDealDamage / OnTakeDamage
    /// use ref int to let effects amplify or reduce damage before it
    /// is applied. ModifyAttributes is a passive stat modifier called
    /// independently of turn phases. OnFinalAction fires once when the
    /// effect expires. All hooks have empty default semantics — effects
    /// override only the hooks they care about.
    /// </summary>
    public interface IStatusActions
    {
        /// <summary>Factory key matching the <c>Actions</c> field in
        /// <see cref="StatusEffectInfo"/>, e.g. <c>"basic/stun"</c>.</summary>
        string ID { get; }
        
        /// <summary>Fires when the owning character's turn begins. Use for
        /// start-of-turn damage-over-time or regeneration effects.</summary>
        void OnTurnStart(Character target);
        /// <summary>Fires when the owning character's turn ends. Mirrors
        /// <see cref="OnTurnStart"/> for symmetry; useful for effects that
        /// activate after the character acts.</summary>
        void OnTurnEnd(Character target);
        /// <summary>Called before the owning character deals damage to
        /// <paramref name="target"/>. Modify <paramref name="damage"/> via
        /// <see langword="ref"/> to amplify or reduce outgoing damage —
        /// all active effects stack multiplicatively through the queue's
        /// iteration.</summary>
        void OnDealDamage(Character source, Character target, ref int damage);
        /// <summary>Called before the owning character receives damage from
        /// <paramref name="source"/>. Modify <paramref name="damage"/> via
        /// <see langword="ref"/> to add damage reduction or vulnerability.
        /// Fires after <see cref="OnDealDamage"/> in the damage
        /// pipeline.</summary>
        void OnTakeDamage(Character target, Character source, ref int damage);
        /// <summary>Fires immediately when the effect is first placed on a
        /// character. Use for one-shot stat boosts, visual feedback, or
        /// initialisation logic.</summary>
        void OnApply(Character target);
        /// <summary>Fires when the effect is manually removed (e.g. via a
        /// cleanse card). Not called on natural expiration — use
        /// <see cref="OnFinalAction"/> for that path.</summary>
        void OnRemove(Character target);
        /// <summary>Fires once when the effect expires naturally (timer reaches
        /// zero). Use for end-of-effect triggers like explosion damage or
        /// stat restoration. Not called on manual removal.</summary>
        void OnFinalAction(Character target);
        /// <summary>Passive stat modifier invoked independently of turn phases.
        /// Receives a reference to the character's attribute container;
        /// implementors can add flat or percentage bonuses that other
        /// effects may also contribute to.</summary>
        void ModifyAttributes(CharacterAttributes attributes);
    }
}
