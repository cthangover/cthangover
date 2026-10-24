namespace Cthangover.Mods.Stats.Formulas
{
    using System;

    /// <summary>
    /// The names a formula can read. Who answers them decides what a formula
    /// means: a stat sheet answers <c>str</c> and <c>level</c> of one character,
    /// a battle skill answers <c>a.atk</c> (the user) and <c>b.def</c> (the
    /// target). Formulas never reach game state any other way, which keeps them
    /// pure data that tools can check and tests can evaluate without a game.
    /// </summary>
    public interface IFormulaScope
    {
        /// <summary>The value of <paramref name="name"/>; false when the scope does not know the name (it then reads as 0).</summary>
        bool TryGet(string name, out double value);

        /// <summary>The dice of <c>rand</c> and <c>chance</c>; a test passes a seeded one.</summary>
        Random Random { get; }
    }
}
