using System.Collections.Generic;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Global animation-player index keyed by AnimationType string.
    /// Mods or cores register players; the action machine looks up the
    /// player for a given animation type. Singleton — there is one set
    /// of players for the entire runtime, but each player can make
    /// per-core decisions internally.
    /// </summary>
    public class AnimationRegistry
    {
        public static readonly AnimationRegistry Instance = new();

        private readonly Dictionary<string, IBattleAnimationPlayer> _players = new();

        public void Register(string animType, IBattleAnimationPlayer player)
        {
            _players[animType] = player;
        }

        public IBattleAnimationPlayer Get(string animType)
        {
            _players.TryGetValue(animType, out var player);
            return player;
        }
    }
}
