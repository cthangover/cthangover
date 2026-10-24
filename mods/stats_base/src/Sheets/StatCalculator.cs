namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>
    /// Computes a <see cref="StatSheet"/> from <see cref="StatInputs"/>. Pure: no game
    /// state, no Godot, so the whole stat model is testable as plain data.
    /// <para/>
    /// Stats are resolved lazily and memoised, in whatever order formulas ask for them, so
    /// a derived stat may read any other stat regardless of definition order. A formula
    /// that reads itself through a chain (<c>a</c> needs <c>b</c> needs <c>a</c>) gets 0
    /// for the looping read and the cycle is logged under <c>STATS</c> once per sheet.
    /// <para/>
    /// The set of stats on a sheet is the union of the definitions, the character's own
    /// entries, the archetype's entries and the traits' stats: a mod may give a character
    /// a stat nobody defined, and it still works in formulas and conditions (undefined
    /// stats are simply not clamped and not shown by the sheet UI).
    /// </summary>
    public class StatCalculator : IFormulaScope
    {
        private readonly StatInputs inputs;
        private readonly string owner;
        private readonly Dictionary<string, double> values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> baseValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> computing = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TraitInfo>> traitsByStat = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> universe = new(StringComparer.OrdinalIgnoreCase);
        private bool cycleLogged;

        private StatCalculator(string owner, StatInputs inputs)
        {
            this.owner = owner;
            this.inputs = inputs;
            foreach (var id in inputs.Definitions.Keys)
                universe.Add(id);
            foreach (var id in inputs.Own.Keys)
                universe.Add(id);
            if (inputs.Archetype?.Stats != null)
            {
                foreach (var id in inputs.Archetype.Stats.Keys)
                    universe.Add(id);
            }
            foreach (var trait in inputs.Traits.Where(t => !string.IsNullOrWhiteSpace(t?.Stat)))
            {
                universe.Add(trait.Stat);
                if (!traitsByStat.TryGetValue(trait.Stat, out var list))
                    traitsByStat[trait.Stat] = list = new List<TraitInfo>();
                list.Add(trait);
            }
        }

        /// <summary>Computes the sheet of <paramref name="characterId"/>.</summary>
        public static StatSheet Calculate(string characterId, StatInputs inputs)
        {
            var calc = new StatCalculator(characterId, inputs ?? new StatInputs());
            foreach (var stat in calc.universe.ToList())
                calc.Value(stat);
            return new StatSheet(characterId, calc.inputs.Level, calc.inputs.Archetype?.ID, calc.values, calc.baseValues);
        }

        /// <inheritdoc />
        public bool TryGet(string name, out double value)
        {
            if (string.Equals(name, "level", StringComparison.OrdinalIgnoreCase))
            {
                value = inputs.Level;
                return true;
            }
            if (name == null || !universe.Contains(name))
            {
                value = 0;
                return false;
            }
            value = Value(name);
            return true;
        }

        /// <inheritdoc />
        public Random Random => Random.Shared;

        private double Value(string stat)
        {
            if (values.TryGetValue(stat, out var done))
                return done;
            if (!computing.Add(stat))
            {
                if (!cycleLogged)
                    GameLogger.Log("STATS", $"{owner}: stat '{stat}' depends on itself through its formulas; the looping read counts as 0", LogLevel.Error);
                cycleLogged = true;
                return 0;
            }

            inputs.Definitions.TryGetValue(stat, out var def);
            var fallback = def?.Default ?? 0;
            var raw = Formula.Evaluate(SourceOf(stat, def), this, fallback);
            if (inputs.Bonus.TryGetValue(stat, out var bonus))
                raw += bonus;
            baseValues[stat] = raw;

            var add = 0d;
            var percent = 0d;
            if (traitsByStat.TryGetValue(stat, out var traits))
            {
                foreach (var trait in traits)
                {
                    add += trait.Add + Formula.Evaluate(trait.Formula, this);
                    percent += trait.Percent;
                }
            }

            var result = (raw + add) * (1 + percent / 100);
            if (def?.Min != null)
                result = Math.Max(result, def.Min.Value);
            if (def?.Max != null)
                result = Math.Min(result, def.Max.Value);

            computing.Remove(stat);
            values[stat] = result;
            return result;
        }

        private string SourceOf(string stat, StatInfo def)
        {
            if (inputs.Own.TryGetValue(stat, out var own))
                return own;
            if (inputs.Archetype?.Stats != null)
            {
                foreach (var pair in inputs.Archetype.Stats)
                {
                    if (string.Equals(pair.Key, stat, StringComparison.OrdinalIgnoreCase))
                        return pair.Value;
                }
            }
            return def?.Formula;
        }
    }
}
