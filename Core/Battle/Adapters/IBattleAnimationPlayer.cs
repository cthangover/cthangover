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
        string AnimationType { get; }

        void Play(IBattleAnimation anim, Action onComplete);
    }
}
