namespace Cthangover.Mods.Stats.Formulas
{
    using System;

    /// <summary>A formula text that cannot be parsed; the message names the position and what was expected.</summary>
    public class FormulaException : Exception
    {
        /// <summary>Creates the error for <paramref name="formula"/> at character <paramref name="position"/>.</summary>
        public FormulaException(string formula, int position, string message)
            : base($"formula '{formula}' at {position}: {message}")
        {
            Position = position;
        }

        /// <summary>Character index in the formula text where parsing stopped.</summary>
        public int Position { get; }
    }
}
