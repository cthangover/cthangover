using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// Central action-execution dispatcher. Resolves an action ID to an
    /// IActionExecutor in two tiers: first the active provider (set by
    /// the current IBattleCore, allowing per-battle-engine overrides),
    /// then a global fallback registry. This two-level lookup lets mods
    /// supply custom executors for specific battle cores without replacing
    /// the global ones. Execute returns ChangedAttributes to allow the
    /// caller to inspect the result (success/failure, stat deltas).
    /// </summary>
    public class ActionExecutorHub
    {
        public static readonly ActionExecutorHub Instance = new();

        private IActionExecutorProvider _activeProvider;
        private readonly Dictionary<string, IActionExecutor> _globalExecutors = new();

        public void SetActiveProvider(IActionExecutorProvider provider)
        {
            _activeProvider = provider;
        }

        public void RegisterGlobal(IActionExecutor executor)
        {
            if (executor != null && !string.IsNullOrEmpty(executor.ActionId))
                _globalExecutors[executor.ActionId] = executor;
        }

        public ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (action == null)
                return new ChangedAttributes { Result = false };

            var executor = Resolve(action.ID);
            if (executor == null)
                return new ChangedAttributes { Result = false };

            return executor.Execute(action, user, target);
        }

        private IActionExecutor Resolve(string actionId)
        {
            if (_activeProvider != null)
            {
                var executor = _activeProvider.GetExecutor(actionId);
                if (executor != null)
                    return executor;
            }

            _globalExecutors.TryGetValue(actionId, out var fallback);
            return fallback;
        }
    }
}
