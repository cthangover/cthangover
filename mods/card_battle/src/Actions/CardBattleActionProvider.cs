using System.Collections.Generic;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.CardBattle.Actions
{
    /// <summary>
    /// Registry that maps action type IDs (<c>"physics/attack"</c>, <c>"physics/defence"</c>, <c>"physics/stun"</c>)
    /// to their respective <see cref="IActionExecutor"/> implementations. Created by <see cref="CardBattleCore"/>
    /// and registered with <see cref="ActionExecutorHub"/> at battle startup so that actions resolved
    /// during drag-and-drop or enemy AI turns go through the correct damage/defence/stun logic.
    /// </summary>
    public class CardBattleActionProvider : IActionExecutorProvider
    {
        private readonly Dictionary<string, IActionExecutor> _executors;

        /// <summary>
        /// Initializes the provider with the three card-battle action executors:
        /// <see cref="PhysicsDamageActionCard"/> for attacks, <see cref="PhysicsDefenceActionCard"/> for defence buffs,
        /// and <see cref="PhysicsStunActionCard"/> for stun effects.
        /// </summary>
        public CardBattleActionProvider()
        {
            _executors = new Dictionary<string, IActionExecutor>
            {
                ["physics/attack"]  = new PhysicsDamageActionCard(),
                ["physics/defence"] = new PhysicsDefenceActionCard(),
                ["physics/stun"]    = new PhysicsStunActionCard(),
            };
        }

        /// <summary>
        /// Looks up the <see cref="IActionExecutor"/> for the given <paramref name="actionId"/>.
        /// Logs a warning and returns <c>null</c> if no executor is registered, allowing the caller
        /// to gracefully skip unrecognized actions.
        /// </summary>
        public IActionExecutor GetExecutor(string actionId)
        {
            if (!_executors.TryGetValue(actionId, out var executor))
                GameLogger.Log("CARD_BATTLE", $"No executor registered for action '{actionId}'", LogLevel.Warning);
            return executor;
        }
    }
}
