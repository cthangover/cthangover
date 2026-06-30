using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    /// <summary>
    /// Defines a status effect that is applied to a <see cref="Character"/> during battle
    /// and processed each turn by <see cref="StatusEffectQueue"/>. Effects receive callbacks
    /// at turn start and turn end, and self-report expiration via <see cref="IsExpired"/>
    /// when their <see cref="Turns"/> counter reaches zero. The <see cref="IStatusActions"/>
    /// callback parameter allows effects to influence the character's turn (e.g. skip it).
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>Integer identifier for the effect type (1 = stun). Used for serialization.</summary>
        int Type { get; }
        /// <summary>Remaining turn count before expiry. Decremented by <see cref="StatusEffectQueue"/>.</summary>
        int Turns { get; set; }
        /// <summary>Returns <c>true</c> when <see cref="Turns"/> ≤ 0, signalling removal from the queue.</summary>
        bool IsExpired { get; }

        /// <summary>Called by <see cref="StatusEffectQueue.OnTurnStart"/> before the turn counter is decremented.</summary>
        void OnTurnStart(Character character, IStatusActions actions);
        /// <summary>Called by <see cref="StatusEffectQueue.OnTurnEnd"/> after the character's actions have resolved.</summary>
        void OnTurnEnd(Character character, IStatusActions actions);
    }
}
