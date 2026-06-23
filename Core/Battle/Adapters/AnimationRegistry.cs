using System.Collections.Generic;

namespace Cthangover.Core.Battle
{
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
