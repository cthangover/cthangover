using System.Collections.Generic;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Registry mapping action ID strings to their executor implementations.
    /// Created by <see cref="FFBattleCore"/> and registered with
    /// <see cref="ActionExecutorHub"/>. Maps <c>"physics/attack"</c> →
    /// <see cref="FFDamageExecutor"/>, <c>"physics/defence"</c> →
    /// <see cref="FFDefenceExecutor"/>, <c>"physics/stun"</c> →
    /// <see cref="FFStunExecutor"/>, and <c>"ff/item"</c> →
    /// <see cref="FFItemExecutor"/>. The lookup key comes from
    /// <see cref="ActionCharacter.ID"/>.
    /// </summary>
    public class FFActionProvider : IActionExecutorProvider
    {
        private readonly Dictionary<string, IActionExecutor> _executors;

        /// <summary>Constructs the provider and registers the four built-in FF battle executors.</summary>
        public FFActionProvider()
        {
            _executors = new Dictionary<string, IActionExecutor>
            {
                ["physics/attack"] = new FFDamageExecutor(),
                ["physics/defence"] = new FFDefenceExecutor(),
                ["physics/stun"] = new FFStunExecutor(),
                ["ff/item"] = new FFItemExecutor(),
            };
        }

        /// <summary>
        /// Retrieves the executor registered for <paramref name="actionId"/>.
        /// Logs a warning if no executor is found and returns <c>null</c>,
        /// which the animation system silently ignores.
        /// </summary>
        /// <param name="actionId">The <see cref="ActionCharacter.ID"/> string to look up.</param>
        public IActionExecutor GetExecutor(string actionId)
        {
            if (!_executors.TryGetValue(actionId, out var executor))
                GameLogger.Log("FF_BATTLE", $"No executor registered for action '{actionId}'", LogLevel.Warning);
            return executor;
        }
    }
}
