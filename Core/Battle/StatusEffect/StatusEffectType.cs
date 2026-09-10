namespace Cthangover.Core.Cards.StatusEffect
{
    /// <summary>
    /// High-level classification for status effects. Buff/Debuff are
    /// informational (UI colouring, stacking rules); Stun is singled
    /// out because the queue has a dedicated HasStun() check that the
    /// battle engine queries to skip a stunned character's turn.
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>Positive effect that benefits the bearer (e.g. stat boost,
        /// regeneration). Rendered with a friendly tint in the UI.</summary>
        Buff,
        /// <summary>Negative effect that harms the bearer (e.g. damage over
        /// time, stat reduction). Rendered with a hostile tint in the
        /// UI.</summary>
        Debuff,
        /// <summary>Crowd-control effect that causes the bearer to skip
        /// their turn. Checked by
        /// <see cref="StatusEffectQueue.HasStun"/>; the action hooks
        /// are deliberately no-ops — the skip is enforced by the battle
        /// engine reading this type flag, not by the
        /// <see cref="IStatusActions"/> callbacks.</summary>
        Stun,
    }
}
