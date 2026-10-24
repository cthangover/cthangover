namespace Cthangover.Mods.Stats
{
    using System;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>The scope of an exp curve: only <c>level</c>, the level being reached.</summary>
    public class LevelScope : IFormulaScope
    {
        private readonly int level;

        /// <summary>Scope for reaching <paramref name="level"/>.</summary>
        public LevelScope(int level)
        {
            this.level = level;
        }

        /// <inheritdoc />
        public bool TryGet(string name, out double value)
        {
            value = level;
            return string.Equals(name, "level", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public Random Random => Random.Shared;
    }
}
