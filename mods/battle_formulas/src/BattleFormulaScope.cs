namespace Cthangover.Mods.BattleFormulas
{
    using System;
    using Cthangover.Core.Characters;
    using Cthangover.Mods.Stats;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>
    /// What a battle formula can read: <c>a.*</c> is the one acting, <c>b.*</c> the target.
    /// Each side answers its stat sheet (<c>a.str</c>, <c>b.mag</c>, <c>a.level</c>) and the
    /// live battle attributes under fixed names — <c>hp</c>, <c>hp_max</c>, <c>attack</c>,
    /// <c>defence</c>, <c>point</c> — so a formula may hit harder on a wounded target
    /// (<c>b.hp &lt; b.hp_max / 2</c>) whatever stats the game defines.
    /// </summary>
    public class BattleFormulaScope : IFormulaScope
    {
        private readonly Character user;
        private readonly Character target;

        /// <summary>Scope of <paramref name="user"/> acting on <paramref name="target"/> (either may be null).</summary>
        public BattleFormulaScope(Character user, Character target, Random random = null)
        {
            this.user = user;
            this.target = target;
            Random = random ?? Random.Shared;
        }

        /// <inheritdoc />
        public Random Random { get; }

        /// <inheritdoc />
        public bool TryGet(string name, out double value)
        {
            value = 0;
            if (name == null || name.Length < 3 || name[1] != '.')
                return false;
            var side = name[0] switch
            {
                'a' => user,
                'b' => target,
                _ => null
            };
            return side != null && Read(side, name.Substring(2), out value);
        }

        /// <summary>A value of one side: live attributes first, then its stat sheet.</summary>
        public static bool Read(Character character, string key, out double value)
        {
            var attributes = character.Attributes;
            switch (key)
            {
                case "hp":
                    value = attributes?.Health?.Value ?? 0;
                    return true;
                case "hp_max":
                    value = attributes?.Health?.BaseValue ?? 0;
                    return true;
                case "attack":
                    value = attributes?.Attack?.Value ?? 0;
                    return true;
                case "defence":
                    value = attributes?.Defence?.Value ?? 0;
                    return true;
                case "point":
                    value = attributes?.Point?.Value ?? 0;
                    return true;
            }
            var sheet = StatsService.Sheet(character.ID);
            if (key == "level")
            {
                value = character.Level > 0 ? character.Level : sheet.Level;
                return true;
            }
            return sheet.TryGet(key, out value);
        }
    }
}
