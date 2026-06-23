using System;
using System.Collections.Generic;
using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    public class StatusEffectActionFactory
    {
        private readonly Dictionary<string, IStatusActions> actions;
        
#region Singleton

        private static readonly Lazy<StatusEffectActionFactory> instance = new(() => new StatusEffectActionFactory());
        public static StatusEffectActionFactory Instance => instance.Value;

        public StatusEffectActionFactory()
        {
            actions = new Dictionary<string, IStatusActions>();
            foreach (var action in Reflections.FindAndCreate<IStatusActions>())
            {
                actions.Add(action.ID, action);
            }
        }
        
#endregion

        public IStatusActions Get(string id)
        {
            if (actions.TryGetValue(id, out var result))
                return result;
            GameLogger.Log("STATUS", "actions '" + id + "' not found!", LogLevel.Error);
            return null;
        }
        
    }
}
