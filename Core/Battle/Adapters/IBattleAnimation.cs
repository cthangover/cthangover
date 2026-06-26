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
        Character Source { get; }
        Character Target { get; }
        string AnimationType { get; }
        float Speed { get; }
        Dictionary<string, object> Parameters { get; }
    }
}
