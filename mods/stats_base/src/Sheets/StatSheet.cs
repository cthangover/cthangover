namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>
    /// The computed stats of one character at one moment: every stat's final value and
    /// the value before traits (so a sheet can show "12 (+3)"). A sheet is immutable;
    /// <see cref="StatsService"/> computes a new one when something it depends on changes.
    /// It is also a formula scope (<c>level</c> and stat ids), which is how conditions,
    /// checks and battle formulas read a character.
    /// </summary>
    public class StatSheet : IFormulaScope
    {
        private readonly Dictionary<string, double> values;
        private readonly Dictionary<string, double> baseValues;

        /// <summary>Creates a sheet; dictionaries are copied with case-insensitive keys.</summary>
        public StatSheet(string characterId, int level, string archetypeId, IDictionary<string, double> values, IDictionary<string, double> baseValues)
        {
            CharacterId = characterId;
            Level = level;
            ArchetypeId = archetypeId;
            this.values = new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);
            this.baseValues = new Dictionary<string, double>(baseValues, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Whose sheet it is (a character id, or the free player id of a game without a party).</summary>
        public string CharacterId { get; }

        /// <summary>The level the sheet was computed for.</summary>
        public int Level { get; }

        /// <summary>The archetype in effect, or null.</summary>
        public string ArchetypeId { get; }

        /// <summary>Final values by stat id.</summary>
        public IReadOnlyDictionary<string, double> Values => values;

        /// <summary>Whether the sheet has the stat.</summary>
        public bool Has(string stat) => stat != null && values.ContainsKey(stat);

        /// <summary>The final value (0 for an unknown stat).</summary>
        public double Get(string stat) => stat != null && values.TryGetValue(stat, out var v) ? v : 0;

        /// <summary>The value before traits (own, archetype or formula value plus permanent bonuses).</summary>
        public double GetBase(string stat) => stat != null && baseValues.TryGetValue(stat, out var v) ? v : 0;

        /// <summary>The final value rounded half away from zero: what battles and checks use.</summary>
        public int Rounded(string stat) => (int)Math.Round(Get(stat), MidpointRounding.AwayFromZero);

        /// <inheritdoc />
        public bool TryGet(string name, out double value)
        {
            if (string.Equals(name, "level", StringComparison.OrdinalIgnoreCase))
            {
                value = Level;
                return true;
            }
            return values.TryGetValue(name ?? string.Empty, out value);
        }

        /// <inheritdoc />
        public Random Random => Random.Shared;
    }
}
