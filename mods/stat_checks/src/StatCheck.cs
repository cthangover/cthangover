namespace Cthangover.Mods.StatChecks
{
    using System;
    using Cthangover.Mods.Stats;

    /// <summary>
    /// One check of a <c>check</c> line: stat value + dice + bonus against a difficulty.
    /// The line is parsed once and the check object lives in the dialog queue, so every
    /// pass through the line (a <c>goto</c> loop, a replay) rolls again and the dialog
    /// variables show the latest roll.
    /// </summary>
    public class StatCheck
    {
        /// <summary>Whose stat (a character id or <c>player</c>).</summary>
        public string Who { get; init; }

        /// <summary>The stat id.</summary>
        public string Stat { get; init; }

        /// <summary>The total needed to succeed.</summary>
        public int Difficulty { get; init; }

        /// <summary>Dice notation (<see cref="DiceRoll"/>).</summary>
        public string Dice { get; init; }

        /// <summary>A flat bonus (or malus) of this check only.</summary>
        public int Bonus { get; init; }

        /// <summary>The stat value of the last roll.</summary>
        public int Value { get; private set; }

        /// <summary>The dice of the last roll.</summary>
        public int Roll { get; private set; }

        /// <summary>Value + dice + bonus of the last roll.</summary>
        public int Total { get; private set; }

        /// <summary>Whether the last roll reached the difficulty.</summary>
        public bool Success { get; private set; }

        /// <summary>Rolls the check now (reads the stat as it is at this moment).</summary>
        public void Perform() => Perform(StatsService.Sheet(Who).Rounded(Stat), null);

        /// <summary>Rolls with a given stat value and dice generator (tests, previews).</summary>
        public void Perform(int value, Random random)
        {
            Value = value;
            Roll = DiceRoll.Roll(Dice, random);
            Total = Value + Roll + Bonus;
            Success = Total >= Difficulty;
        }
    }
}
