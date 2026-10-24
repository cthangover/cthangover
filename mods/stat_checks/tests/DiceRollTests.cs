namespace Cthangover.Mods.StatChecks.Tests
{
    using System;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Tabletop dice notation: valid forms roll within their range, "no dice" rolls 0, typos roll 0 and are invalid.</summary>
    public class DiceRollTests
    {
        [ModTest("dice/notation")]
        static void Notation(ModTestContext ctx)
        {
            ModAssert.True(DiceRoll.TryParse("2d6", out var count, out var sides) && count == 2 && sides == 6, "2d6");
            ModAssert.True(DiceRoll.TryParse("D20", out count, out sides) && count == 1 && sides == 20, "d20 in any case");
            ModAssert.True(DiceRoll.TryParse("none", out count, out _) && count == 0, "none");
            ModAssert.True(DiceRoll.TryParse("", out count, out _) && count == 0, "empty");
            foreach (var bad in new[] { "d1", "2x6", "-1d6", "d", "3d", "1000d6", "1.5d6" })
                ModAssert.False(DiceRoll.IsValid(bad), $"'{bad}' is not dice");
        }

        [ModTest("dice/rolls_stay_in_range")]
        static void RollsStayInRange(ModTestContext ctx)
        {
            var random = new Random(3);
            var seenLow = false;
            var seenHigh = false;
            for (var i = 0; i < 400; i++)
            {
                var roll = DiceRoll.Roll("2d6", random);
                ModAssert.True(roll >= 2 && roll <= 12, $"2d6 gave {roll}");
                seenLow |= roll == 2;
                seenHigh |= roll == 12;
            }
            ModAssert.True(seenLow && seenHigh, "both ends of 2d6 come up in 400 rolls");
            ModAssert.Equal(0, DiceRoll.Roll("none"), "no dice");
            ModAssert.Equal(0, DiceRoll.Roll("oops"), "a typo rolls nothing");
        }
    }
}
