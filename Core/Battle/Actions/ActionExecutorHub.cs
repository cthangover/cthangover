using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle.Actions
{
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
