using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Describes a battle animation without binding to a concrete player.
    /// Carries Source/Target characters, an AnimationType key for dispatch,
    /// a Speed multiplier, and an open-ended Parameters dictionary so
    /// custom animations can pass arbitrary data (damage value, element
    /// type, etc.) without interface changes.
    /// </summary>
    public interface IBattleAnimation
    {
        /// <summary>The <see cref="Character"/> performing the action.</summary>
        Character Source { get; }
        /// <summary>The <see cref="Character"/> receiving the action.</summary>
        Character Target { get; }
        /// <summary>
        /// Key used by <see cref="AnimationRegistry"/> to dispatch to the
        /// correct <see cref="IBattleAnimationPlayer"/>.
        /// </summary>
        string AnimationType { get; }
        /// <summary>Multiplier for the playback speed (1.0 = normal).</summary>
        float Speed { get; }
        /// <summary>
        /// Open-ended data bag for animation-specific values
        /// (e.g. damage amount, elemental flag) — no interface changes
        /// needed when adding new animation types.
        /// </summary>
        Dictionary<string, object> Parameters { get; }
    }
}
