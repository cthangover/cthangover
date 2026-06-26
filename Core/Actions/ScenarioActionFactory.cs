using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Singleton registry that discovers and indexes all IScenarioAction
    /// implementations via reflection. Actions are keyed by their Name property
    /// (e.g. "quest.set_status"). The factory uses Lazy&lt;T&gt; for thread-safe
    /// initialization via Reflections.FindAndCreate which scans all loaded
    /// assemblies. RegisterAssembly allows mods to inject custom actions at
    /// runtime — deduplication by Name ensures mods can override core actions
    /// by registering first. The factory is consumed by ActionScenario (dialog
    /// action) which bridges the dialog system to these atomic scenario commands.
    /// </summary>
    public class ScenarioActionFactory
    {
        private static readonly Lazy<ScenarioActionFactory> instance = new(() => new ScenarioActionFactory());

        public static ScenarioActionFactory Instance => instance.Value;

        private readonly Dictionary<string, IScenarioAction> actions = new();

        private ScenarioActionFactory()
        {
            foreach (var action in Reflections.FindAndCreate<IScenarioAction>())
            {
                Register(action);
            }

            GameLogger.Log("FACTORY", $"loaded '{actions.Count}' scenario actions");
        }

        public IScenarioAction Get(string name)
        {
            if (actions.TryGetValue(name, out var action))
                return action;

            GameLogger.Log("FACTORY", $"ScenarioAction '{name}' not found", LogLevel.Error);
            return null;
        }

        public void Register(IScenarioAction action)
        {
            if (actions.ContainsKey(action.Name))
                return;
            
            actions.Add(action.Name, action);
            GameLogger.Log("FACTORY", $"registered scenario action '{action.Name}'");
        }

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IScenarioAction).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var action = (IScenarioAction)Activator.CreateInstance(type);
                    Register(action);
                }
            }
        }

        public List<IScenarioAction> GetAll() => actions.Values.ToList();
    }
}
