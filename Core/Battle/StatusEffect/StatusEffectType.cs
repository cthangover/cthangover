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
        Buff,
        Debuff,
        Stun,
    }
}
