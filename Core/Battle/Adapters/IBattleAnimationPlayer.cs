using System;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Plays a specific animation type. Each player is keyed by
    /// AnimationType so the registry can dispatch animations without
    /// knowing concrete player types. The onComplete callback chains
    /// into the action machine's polling loop.
    /// </summary>
    public interface IBattleAnimationPlayer
    {
        /// <summary>
        /// String key used by <see cref="AnimationRegistry"/> to select
        /// this player for a given <see cref="IBattleAnimation.AnimationType"/>.
        /// </summary>
        string AnimationType { get; }

        /// <summary>
        /// Starts playing the animation described by <paramref name="anim"/>.
        /// Must invoke <paramref name="onComplete"/> once the animation
        /// finishes, so the action machine can advance to the next step.
        /// </summary>
        void Play(IBattleAnimation anim, Action onComplete);
    }
}
