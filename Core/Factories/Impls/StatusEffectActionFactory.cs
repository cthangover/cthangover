using System;
using System.Collections.Generic;
using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Reflection-based plugin registry for status effect behaviour
    /// classes, mirroring the pattern used by <c>ItemActionFactory</c>.
    /// Each <c>IStatusActions</c> implementation corresponds to a status
    /// effect ID referenced in battle data JSON — the factory auto-discovers
    /// them at startup so mod authors can add new status effects (e.g.
    /// poison, burn, freeze) by implementing the interface in their
    /// assembly without touching core code.
    /// </summary>
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
