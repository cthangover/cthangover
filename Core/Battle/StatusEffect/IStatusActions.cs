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
