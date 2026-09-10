using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{

    /// <summary>
    /// Status effect data contract. Separates identity (ID, Name, Icon)
    /// from mutable state (Duration, RemainingTurns) and behaviour
    /// (Actions — an IStatusActions hook set). Copy() is required so the
    /// StatusEffectQueue can clone effects when characters are duplicated.
    /// </summary>
    public interface IStatusEffect
    {
        /// <summary>Unique mod identifier, e.g. <c>"basic/stun"</c>. Used by factories
        /// and the queue to look up and remove effects by key.</summary>
        string ID { get; }
        /// <summary>Display name shown in the battle UI.</summary>
        string Name { get; }
        /// <summary>Flavour text explaining the effect to the player.</summary>
        string Description { get; }
        /// <summary>High-level category (<c>Buff</c>, <c>Debuff</c>, <c>Stun</c>) used
        /// for UI tinting and quick type checks like
        /// <see cref="StatusEffectQueue.HasStun"/>.</summary>
        StatusEffectType EffectType { get; }
        /// <summary>Base duration in turns. Set during effect creation; may be
        /// overridden by the caller via <see cref="StatusEffectQueue.Add"/>.
        /// A value of 0 or negative means the effect never expires
        /// naturally.</summary>
        int Duration { get; set; }
        /// <summary>Turns remaining before the effect expires. Decremented each
        /// turn boundary by the queue. Independent of <c>Duration</c> so
        /// that the original value is preserved for display or re-apply
        /// logic.</summary>
        int RemainingTurns { get; set; }
        /// <summary>Icon texture loaded from the <c>"characters"</c> mod group,
        /// displayed beside the effect name in the HUD.</summary>
        Texture2D Icon { get; }
        /// <summary>Behaviour hooks that fire during turn phases and on damage
        /// events. Resolved by <c>StatusEffectActionFactory</c> from the
        /// <see cref="StatusEffectInfo.Actions"/> string ID.</summary>
        IStatusActions Actions { get; }

        /// <summary>Creates a shallow clone. <c>Actions</c> is a shared reference
        /// (stateless singleton) so it is not deep-copied. Used by
        /// <see cref="StatusEffectQueue.Copy"/> when duplicating a
        /// character.</summary>
        IStatusEffect Copy();
    }
}
