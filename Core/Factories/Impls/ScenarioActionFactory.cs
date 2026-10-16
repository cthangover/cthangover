using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Actions;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Singleton registry that discovers and indexes all IScenarioAction
    /// implementations via reflection. Actions are keyed by their Name property
    /// (e.g. "quest.set_status"). The single instance is owned by the
    /// AppServices composition root in a Lazy&lt;T&gt; slot for thread-safe
    /// initialization via Reflections.FindAndCreate which scans all loaded
    /// assemblies. RegisterAssembly allows mods to inject custom actions at
    /// runtime — deduplication by Name ensures mods can override core actions
    /// by registering first. The factory is consumed by ActionScenario (dialog
    /// action) which bridges the dialog system to these atomic scenario commands.
    /// </summary>
    public class ScenarioActionFactory
    {
        /// <summary>
        /// Facade over the AppServices-owned instance; kept as the public
        /// API during migration. Laziness and thread safety come from the
        /// owning Lazy&lt;T&gt; slot. Construction triggers a full assembly scan
        /// for all IScenarioAction implementations via
        /// Reflections.FindAndCreate and registers each by its Name.
        /// </summary>
        public static ScenarioActionFactory Instance => AppServices.Current.ScenarioActions.Value;

        private readonly Dictionary<string, IScenarioAction> actions = new();

        /// <summary>
        /// Guards <see cref="actions"/>. The factory is a shared singleton reached from
        /// parallel test collections and from GDScript registration paths; without the
        /// lock a GetAll snapshot racing a Register resize throws
        /// "Destination array is not long enough" — observed as a rare flake in
        /// ScenarioActionFactoryTests.
        /// </summary>
        private readonly object actionsLock = new();

        internal ScenarioActionFactory()
        {
            foreach (var action in Reflections.FindAndCreate<IScenarioAction>())
            {
                Register(action);
            }

            lock (actionsLock)
            {
                GameLogger.Log("FACTORY", $"loaded '{actions.Count}' scenario actions");
            }
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
            lock (actionsLock)
            {
                if (actions.TryGetValue(name, out var action))
                    return action;
            }

            GameLogger.Log("FACTORY", $"ScenarioAction '{name}' not found", LogLevel.Error);
            return null;
        }

        /// <summary>
        /// Looks up a registered action by its Name and fails fast when it is
        /// missing. This is the fail-fast twin of <see cref="Get"/>: scenario
        /// content playback must stay resilient (a typo in a mod's scenario
        /// degrades to an error log and a skipped command), but engine-critical
        /// code — tools, boot wiring, validators, tests — resolves actions by
        /// names that are contractually known or already validated, and there a
        /// null return would only postpone the failure to a harder-to-trace
        /// spot. The same Get/GetRequired split exists in
        /// <c>CharacterTemplateRegistry</c>; <c>BattleCoreRegistry</c> throws
        /// unconditionally for the same reason.
        /// </summary>
        /// <exception cref="KeyNotFoundException">No action with the given Name is registered.</exception>
        public IScenarioAction GetRequired(string name)
        {
            lock (actionsLock)
            {
                if (actions.TryGetValue(name, out var action))
                    return action;

                throw new KeyNotFoundException(
                    $"Scenario action '{name}' is not registered ({actions.Count} actions loaded). " +
                    "Check the action Name spelling or make sure the mod that defines it is loaded.");
            }
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
            lock (actionsLock)
            {
                if (actions.ContainsKey(action.Name))
                    return;

                actions.Add(action.Name, action);
            }

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
            foreach (var action in Reflections.FindAndCreate<IScenarioAction>(assembly))
            {
                Register(action);
            }
        }

        /// <summary>
        /// Removes actions whose concrete type comes from the given assembly —
        /// the unload pass of the assembly consumer pipeline. Core actions
        /// survive; a mod reinstall re-registers through the normal fan-out.
        /// </summary>
        public void UnregisterAssembly(Assembly assembly)
        {
            lock (actionsLock)
            {
                foreach (var name in actions.Where(kvp => kvp.Value.GetType().Assembly == assembly).Select(kvp => kvp.Key).ToList())
                    actions.Remove(name);
            }
        }

        /// <summary>
        /// Returns a snapshot list of all registered actions. Used for
        /// debugging and introspection (listing available commands).
        /// Not intended for hot-path lookups — use Get() by name for
        /// that.
        /// </summary>
        public List<IScenarioAction> GetAll()
        {
            lock (actionsLock)
                return actions.Values.ToList();
        }
    }
}
