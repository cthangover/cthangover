using System.Collections.Generic;
using Cthangover.Mods.Dialog.Scenarios;
using Cthangover.Core.Utils;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Registry of <see cref="IScenarioCommandStrategy"/> implementations,
    /// indexed by their <see cref="IScenarioCommandStrategy.Command"/> name.
    /// It discovers nothing itself: <see cref="ScenarioCommandStrategyConsumer"/>
    /// delivers the strategies of every mod as the kernel registers its
    /// assembly and takes them back when the mod unloads, so the table never
    /// holds a strategy of a dead assembly (which would pin it and keep running
    /// its old code after a hot reload).
    /// </summary>
    public static class ScenarioCommandStrategyFactory
    {
        private static readonly Dictionary<string, IScenarioCommandStrategy> Strategies = new();

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
        /// Registers a strategy under its command name. The first strategy of a
        /// command wins: a mod cannot silently take over a built-in command.
        /// </summary>
        public static void Register(IScenarioCommandStrategy strategy)
        {
            if (strategy != null && !Strategies.ContainsKey(strategy.Command))
                Strategies[strategy.Command] = strategy;
        }

        /// <summary>
        /// Removes <paramref name="strategy"/> (its mod is being unloaded). A
        /// command owned by another strategy instance is left alone.
        /// </summary>
        public static void Unregister(IScenarioCommandStrategy strategy)
        {
            if (strategy != null && Strategies.TryGetValue(strategy.Command, out var registered) && registered == strategy)
                Strategies.Remove(strategy.Command);
        }
    }
}
