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
        /// <summary>
        /// Singleton registry. One set of animation players serves the
        /// entire runtime; individual players dispatch per-core via
        /// internal logic.
        /// </summary>
        public static readonly AnimationRegistry Instance = new();

        private readonly Dictionary<string, IBattleAnimationPlayer> _players = new();

        /// <summary>
        /// Registers a player under <paramref name="animType"/>.
        /// Replaces any existing player for the same key — last write wins.
        /// </summary>
        public void Register(string animType, IBattleAnimationPlayer player)
        {
            _players[animType] = player;
        }

        /// <summary>
        /// Retrieves the player registered for <paramref name="animType"/>,
        /// or null if no match exists.
        /// </summary>
        public IBattleAnimationPlayer Get(string animType)
        {
            _players.TryGetValue(animType, out var player);
            return player;
        }
    }
}
