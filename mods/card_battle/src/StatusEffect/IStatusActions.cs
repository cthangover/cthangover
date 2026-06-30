namespace Cthangover.CardBattle.StatusEffect
{
    /// <summary>
    /// Interface exposed by <see cref="StatusEffectQueue"/> to <see cref="IStatusEffect"/> instances
    /// so that status effects can communicate back to the queue during turn lifecycle callbacks.
    /// <c>SkipTurn</c> allows an effect (e.g. stun) to mark the character's entire turn as skipped.
    /// <c>RemoveStatus</c> allows an effect to self-remove from the queue.
    /// </summary>
    public interface IStatusActions
    {
        /// <summary>
        /// Marks the character's current turn as skipped. Called by <see cref="StunStatusEffect.OnTurnStart"/>.
        /// </summary>
        void SkipTurn();
        /// <summary>
        /// Removes the specified status effect from the queue. Called by effects that want to
        /// self-terminate before their turn count expires.
        /// </summary>
        void RemoveStatus(IStatusEffect status);
    }
}
