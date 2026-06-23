using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle
{
    public interface IBattleAnimation
    {
        Character Source { get; }
        Character Target { get; }
        string AnimationType { get; }
        float Speed { get; }
        Dictionary<string, object> Parameters { get; }
    }
}
