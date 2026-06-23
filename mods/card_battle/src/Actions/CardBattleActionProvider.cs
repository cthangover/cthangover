using System.Collections.Generic;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.CardBattle.Actions
{
    public class CardBattleActionProvider : IActionExecutorProvider
    {
        private readonly Dictionary<string, IActionExecutor> _executors;

        public CardBattleActionProvider()
        {
            _executors = new Dictionary<string, IActionExecutor>
            {
                ["physics/attack"]  = new PhysicsDamageActionCard(),
                ["physics/defence"] = new PhysicsDefenceActionCard(),
                ["physics/stun"]    = new PhysicsStunActionCard(),
            };
        }

        public IActionExecutor GetExecutor(string actionId)
        {
            if (!_executors.TryGetValue(actionId, out var executor))
                GameLogger.Log("CARD_BATTLE", $"No executor registered for action '{actionId}'", LogLevel.Warning);
            return executor;
        }
    }
}
