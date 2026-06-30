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
        /// <summary>
        /// Singleton hub instance. There is one global dispatcher shared
        /// across all battle cores; cores swap providers via
        /// <see cref="SetActiveProvider"/> when they activate.
        /// </summary>
        public static readonly ActionExecutorHub Instance = new();

        private IActionExecutorProvider _activeProvider;
        private readonly Dictionary<string, IActionExecutor> _globalExecutors = new();

        /// <summary>
        /// Installs a per-core executor provider. The active provider is
        /// consulted before the global registry, allowing the current
        /// battle core to override specific action handlers.
        /// </summary>
        public void SetActiveProvider(IActionExecutorProvider provider)
        {
            _activeProvider = provider;
        }

        /// <summary>
        /// Registers a fallback executor in the global pool, keyed by
        /// <see cref="IActionExecutor.ActionId"/>. Global executors are
        /// used when no active provider supplies a match.
        /// </summary>
        public void RegisterGlobal(IActionExecutor executor)
        {
            if (executor != null && !string.IsNullOrEmpty(executor.ActionId))
                _globalExecutors[executor.ActionId] = executor;
        }

        /// <summary>
        /// Dispatches an action to the appropriate executor. Resolution
        /// order: active provider first, then global registry. Returns
        /// <c>Result = false</c> if no executor is found or if
        /// <paramref name="action"/> is null.
        /// </summary>
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
