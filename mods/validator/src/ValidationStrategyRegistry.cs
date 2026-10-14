using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Discovers all <see cref="IValidationStrategy"/> implementations in the
    /// loaded assemblies through the project-wide reflection plugin mechanism
    /// and returns them in deterministic execution order. The list is cached for
    /// the process lifetime because mod assemblies are never unloaded; adding a
    /// new strategy (for example scenario validation) is just shipping a new
    /// public class with a parameterless constructor.
    /// </summary>
    public static class ValidationStrategyRegistry
    {
        private static readonly object sync = new();
        private static IReadOnlyList<IValidationStrategy> cached;

        /// <summary>Returns all discovered strategies sorted by phase and order; cached for the process lifetime.</summary>
        public static IReadOnlyList<IValidationStrategy> GetStrategies()
        {
            lock (sync)
            {
                if (cached != null)
                    return cached;

                var list = new List<IValidationStrategy>(Reflections.FindAndCreate<IValidationStrategy>());
                list.Sort(Compare);
                cached = list;
                return cached;
            }
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
