using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Auto-discovery registry for <see cref="IScenarioCommandStrategy"/> implementations.
    /// On static initialization, scans the assembly via <c>Reflections.FindAndCreate</c>
    /// and indexes all strategy instances by their <see cref="IScenarioCommandStrategy.Command"/>
    /// name. Also supports runtime registration of external assemblies for plugin scenarios.
    /// </summary>
    public static class ScenarioCommandStrategyFactory
    {
        private static readonly Dictionary<string, IScenarioCommandStrategy> Strategies;

        static ScenarioCommandStrategyFactory()
        {
            Strategies = new();
            foreach (var strategy in Reflections.FindAndCreate<IScenarioCommandStrategy>())
                Strategies[strategy.Command] = strategy;
        }

        /// <summary>
        /// Looks up the <see cref="IScenarioCommandStrategy"/> registered for <paramref name="command"/>.
        /// Returns <c>null</c> if no strategy exists for the given command name.
        /// </summary>
        public static IScenarioCommandStrategy Get(string command)
        {
            Strategies.TryGetValue(command, out var strategy);
            return strategy;
        }

        /// <summary>
        /// Enumerates all registered DSL command names (lowercase).
        /// </summary>
        public static IEnumerable<string> GetAllCommandNames()
        {
            return Strategies.Keys;
        }

        /// <summary>
        /// Returns the <see cref="ICommandReferenceMetadata"/> for a command if its
        /// strategy implements that interface, or <c>null</c> otherwise. Used by the
        /// build pipeline to collect asset dependencies from scenario scripts.
        /// </summary>
        public static ICommandReferenceMetadata GetReferenceMetadata(string command)
        {
            if (Strategies.TryGetValue(command, out var strategy) && strategy is ICommandReferenceMetadata meta)
                return meta;
            return null;
        }

        /// <summary>
        /// Scans a plugin assembly for public <see cref="IScenarioCommandStrategy"/>
        /// types, instantiates them, and registers them into the strategy table.
        /// Duplicate command names are silently ignored.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IScenarioCommandStrategy).IsAssignableFrom(type) && !type.IsAbstract && type.IsPublic)
                {
                    var strategy = (IScenarioCommandStrategy)System.Activator.CreateInstance(type);
                    if (!Strategies.ContainsKey(strategy.Command))
                        Strategies[strategy.Command] = strategy;
                }
            }
        }
    }
}
