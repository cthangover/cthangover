using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
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

        /// <summary>
        /// Thread-safe singleton instance, lazily initialized via
        /// Lazy&lt;T&gt;. Construction triggers a full assembly scan
        /// for all IScenarioAction implementations via
        /// Reflections.FindAndCreate and registers each by its Name.
        /// </summary>
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

        /// <summary>
        /// Looks up a registered action by its Name (e.g.
        /// "quest.set_status"). Returns null and logs an error if no
        /// action with the given name is registered. This is the primary
        /// entry point for the dialog engine — ActionScenario calls Get()
        /// with the command name from the scenario script and invokes Run()
        /// on the returned action.
        /// </summary>
        public IScenarioAction Get(string name)
        {
            if (actions.TryGetValue(name, out var action))
                return action;

            GameLogger.Log("FACTORY", $"ScenarioAction '{name}' not found", LogLevel.Error);
            return null;
        }

        /// <summary>
        /// Registers an action instance. If an action with the same Name
        /// is already registered, the new one is silently ignored — this
        /// allows core actions to take precedence over mod overrides,
        /// and prevents accidental duplicates from reflection scans.
        /// Logs the registration for debugging.
        /// </summary>
        public void Register(IScenarioAction action)
        {
            if (actions.ContainsKey(action.Name))
                return;
            
            actions.Add(action.Name, action);
            GameLogger.Log("FACTORY", $"registered scenario action '{action.Name}'");
        }

        /// <summary>
        /// Scans all types in the given assembly for non-abstract
        /// IScenarioAction implementations, instantiates each via
        /// Activator.CreateInstance, and registers them. This is the
        /// extension point for mods — mod assemblies call this at
        /// startup to inject custom scenario actions into the dialog
        /// engine. Actions with names already registered are skipped.
        /// </summary>
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

        /// <summary>
        /// Returns a snapshot list of all registered actions. Used for
        /// debugging and introspection (listing available commands).
        /// Not intended for hot-path lookups — use Get() by name for
        /// that.
        /// </summary>
        public List<IScenarioAction> GetAll() => actions.Values.ToList();
    }
}
