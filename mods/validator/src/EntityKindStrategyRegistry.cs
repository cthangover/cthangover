using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Discovers every <see cref="IEntityKindStrategy"/> in the loaded assemblies
    /// through the project-wide reflection plugin mechanism and indexes it by
    /// the entity kind it handles. The map is cached for the process lifetime
    /// because mod assemblies are never unloaded; adding kind handling is just
    /// shipping a new public class with a parameterless constructor.
    /// </summary>
    public static class EntityKindStrategyRegistry
    {
        private static readonly object sync = new();
        private static Dictionary<string, IEntityKindStrategy> cached;

        /// <summary>
        /// Returns the strategy registered for the entity kind, or <c>null</c>
        /// when no strategy claims it — the caller reports the unknown kind as a
        /// validation warning.
        /// </summary>
        public static IEntityKindStrategy Resolve(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return null;

            var strategies = GetStrategies();
            strategies.TryGetValue(kind, out var strategy);
            return strategy;
        }

        private static Dictionary<string, IEntityKindStrategy> GetStrategies()
        {
            lock (sync)
            {
                if (cached != null)
                    return cached;

                var map = new Dictionary<string, IEntityKindStrategy>(StringComparer.OrdinalIgnoreCase);
                foreach (var strategy in Reflections.FindAndCreate<IEntityKindStrategy>())
                {
                    if (string.IsNullOrWhiteSpace(strategy.Kind))
                        continue;

                    if (map.TryGetValue(strategy.Kind, out var existing))
                    {
                        GameLogger.Log("VALIDATOR",
                            $"entity kind '{strategy.Kind}' is handled by both '{existing.GetType().Name}' and '{strategy.GetType().Name}'; keeping the first",
                            LogLevel.Error);
                        continue;
                    }

                    map[strategy.Kind] = strategy;
                }

                cached = map;
                return cached;
            }
        }
    }
}
