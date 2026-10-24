namespace Cthangover.Mods.Stats.Formulas
{
    using System.Collections.Concurrent;
    using System.Globalization;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Entry point of the formula language: texts like <c>"30 + vit * 6 + level * 4"</c>
    /// or <c>"max(1, a.atk * 2 - b.def)"</c> that live in data (stat definitions,
    /// archetypes, traits, battle actions, exp curves) instead of C#.
    /// <para/>
    /// Parsed trees are cached per text for the whole session (formulas are a closed set
    /// written in data, so the cache stays small). A text that does not parse is logged
    /// once under <c>STATS</c> and evaluates as 0, so a broken formula in a mod shows up
    /// in the log and the validator instead of throwing in the middle of play.
    /// </summary>
    public static class Formula
    {
        private static readonly ConcurrentDictionary<string, FormulaNode> Cache = new();
        private static readonly ConcurrentDictionary<string, string> Broken = new();

        /// <summary>The parsed tree (cached); throws <see cref="FormulaException"/> on a syntax error.</summary>
        public static FormulaNode Parse(string text)
            => Cache.GetOrAdd(text ?? string.Empty, FormulaParser.Parse);

        /// <summary>Parses without throwing: false and the parser's message for a broken text.</summary>
        public static bool TryParse(string text, out FormulaNode node, out string error)
        {
            try
            {
                node = Parse(text);
                error = null;
                return true;
            }
            catch (FormulaException ex)
            {
                node = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Evaluates <paramref name="text"/> in <paramref name="scope"/>. A plain number is
        /// read directly; an empty text gives <paramref name="fallback"/>; a broken one is
        /// logged once and gives the fallback too.
        /// </summary>
        public static double Evaluate(string text, IFormulaScope scope, double fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;
            if (TryParse(text, out var node, out var error))
                return node.Evaluate(scope);
            if (Broken.TryAdd(text, error))
                GameLogger.Log("STATS", error, LogLevel.Error);
            return fallback;
        }
    }
}
