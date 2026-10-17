using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Registry of <see cref="IEntityKindStrategy"/> implementations, indexed by
    /// the entity kind each one handles. It discovers nothing itself:
    /// <see cref="EntityKindStrategyConsumer"/> delivers the strategies of every
    /// mod as the kernel registers its assembly and takes them back when the mod
    /// unloads, so the map never holds a strategy of a dead assembly. Adding kind
    /// handling is just shipping a new public class with a parameterless
    /// constructor in any mod.
    /// </summary>
    public static class EntityKindStrategyRegistry
    {
        private static readonly object sync = new();
        private static readonly Dictionary<string, IEntityKindStrategy> strategies = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the strategy registered for the entity kind, or <c>null</c>
        /// when no strategy claims it — the caller reports the unknown kind as a
        /// validation warning.
        /// </summary>
        public static IEntityKindStrategy Resolve(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return null;

            lock (sync)
            {
                strategies.TryGetValue(kind, out var strategy);
                return strategy;
            }
        }

        /// <summary>
        /// Registers a strategy under its kind. The first strategy of a kind
        /// wins; a second one is reported as an error and ignored.
        /// </summary>
        public static void Register(IEntityKindStrategy strategy)
        {
            if (strategy == null || string.IsNullOrWhiteSpace(strategy.Kind))
                return;

            lock (sync)
            {
                if (strategies.TryGetValue(strategy.Kind, out var existing))
                {
                    GameLogger.Log("VALIDATOR",
                        $"entity kind '{strategy.Kind}' is handled by both '{existing.GetType().Name}' and '{strategy.GetType().Name}'; keeping the first",
                        LogLevel.Error);
                    return;
                }

                strategies[strategy.Kind] = strategy;
            }
        }

        /// <summary>
        /// Removes <paramref name="strategy"/> (its mod is being unloaded). A kind
        /// owned by another strategy instance is left alone.
        /// </summary>
        public static void Unregister(IEntityKindStrategy strategy)
        {
            if (strategy == null || string.IsNullOrWhiteSpace(strategy.Kind))
                return;

            lock (sync)
            {
                if (strategies.TryGetValue(strategy.Kind, out var registered) && registered == strategy)
                    strategies.Remove(strategy.Kind);
            }
        }
    }
}
