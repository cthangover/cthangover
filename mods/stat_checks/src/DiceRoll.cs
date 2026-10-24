namespace Cthangover.Mods.StatChecks
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Dice in tabletop notation: <c>d20</c>, <c>2d6</c>, <c>1d100</c>; <c>none</c> (or
    /// empty, or <c>0</c>) rolls nothing, which makes a check deterministic — a novel's
    /// "you are brave enough, or you are not". A malformed text rolls nothing too, so a
    /// typo never breaks a scene; the validator reports it.
    /// </summary>
    public static class DiceRoll
    {
        /// <summary>Whether <paramref name="dice"/> is valid notation (or a "no dice" word).</summary>
        public static bool IsValid(string dice) => TryParse(dice, out _, out _);

        /// <summary>Rolls <paramref name="dice"/>: the sum of the faces, 0 for no dice.</summary>
        public static int Roll(string dice, Random random = null)
        {
            if (!TryParse(dice, out var count, out var sides) || count == 0)
                return 0;
            var rng = random ?? Random.Shared;
            var sum = 0;
            for (var i = 0; i < count; i++)
                sum += rng.Next(1, sides + 1);
            return sum;
        }

        /// <summary>Parses <c>NdM</c>; <paramref name="count"/> is 0 for "no dice".</summary>
        public static bool TryParse(string dice, out int count, out int sides)
        {
            count = 0;
            sides = 0;
            var text = dice?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text) || text is "none" or "0")
                return true;
            var d = text.IndexOf('d');
            if (d < 0)
                return false;
            var countText = text.Substring(0, d);
            if (countText.Length == 0)
                count = 1;
            else if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count))
                return false;
            if (!int.TryParse(text.Substring(d + 1), NumberStyles.None, CultureInfo.InvariantCulture, out sides))
                return false;
            return count is >= 1 and <= 100 && sides >= 2;
        }
    }
}
