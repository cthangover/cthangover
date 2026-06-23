using System;

namespace Cthangover.Core.Battle
{
    public interface IBattleAnimationPlayer
    {
        string AnimationType { get; }

        void Play(IBattleAnimation anim, Action onComplete);
    }
}
