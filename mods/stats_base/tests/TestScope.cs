namespace Cthangover.Mods.Stats.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>A formula scope over a plain dictionary and a seeded generator, for the tests.</summary>
    public class TestScope : IFormulaScope
    {
        private readonly Dictionary<string, double> values;

        public TestScope(Dictionary<string, double> values, int seed = 7)
        {
            this.values = new Dictionary<string, double>(values ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase);
            Random = new Random(seed);
        }

        public bool TryGet(string name, out double value) => values.TryGetValue(name, out value);

        public Random Random { get; }
    }
}
