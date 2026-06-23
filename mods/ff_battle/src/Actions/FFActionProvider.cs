using System.Collections.Generic;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    public class FFActionProvider : IActionExecutorProvider
    {
        private readonly Dictionary<string, IActionExecutor> _executors;

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

        public IActionExecutor GetExecutor(string actionId)
        {
            if (!_executors.TryGetValue(actionId, out var executor))
                GameLogger.Log("FF_BATTLE", $"No executor registered for action '{actionId}'", LogLevel.Warning);
            return executor;
        }
    }
}
