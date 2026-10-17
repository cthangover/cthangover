using System;
using System.Collections.Generic;

namespace Cthangover.Validator
{
    /// <summary>
    /// Registry of all <see cref="IValidationStrategy"/> implementations, served
    /// in deterministic execution order. It discovers nothing itself:
    /// <see cref="ValidationStrategyConsumer"/> delivers the strategies of every
    /// mod as the kernel registers its assembly and takes them back when the mod
    /// unloads (game switch, hot reload), so a validation run never executes a
    /// strategy of a dead assembly. Adding a new strategy (for example scenario
    /// validation) is just shipping a new public class with a parameterless
    /// constructor in any mod.
    /// </summary>
    public static class ValidationStrategyRegistry
    {
        private static readonly object sync = new();
        private static readonly List<IValidationStrategy> strategies = new();

        /// <summary>Returns a snapshot of the registered strategies sorted by phase and order.</summary>
        public static IReadOnlyList<IValidationStrategy> GetStrategies()
        {
            lock (sync)
                return strategies.ToArray();
        }

        /// <summary>Adds a strategy and keeps the execution order.</summary>
        public static void Register(IValidationStrategy strategy)
        {
            if (strategy == null)
                return;
            lock (sync)
            {
                strategies.Add(strategy);
                strategies.Sort(Compare);
            }
        }

        /// <summary>Removes exactly <paramref name="strategy"/> (its mod is being unloaded).</summary>
        public static void Unregister(IValidationStrategy strategy)
        {
            lock (sync)
                strategies.Remove(strategy);
        }

        private static int Compare(IValidationStrategy a, IValidationStrategy b)
        {
            var phase = a.Phase.CompareTo(b.Phase);
            if (phase != 0)
                return phase;

            var order = a.Order.CompareTo(b.Order);
            if (order != 0)
                return order;

            return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
