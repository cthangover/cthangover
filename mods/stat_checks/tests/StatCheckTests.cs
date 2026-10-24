namespace Cthangover.Mods.StatChecks.Tests
{
    using System;
    using Cthangover.Core.Mods.Testing;

    /// <summary>A check is value + dice + bonus against the difficulty; reaching it exactly succeeds.</summary>
    public class StatCheckTests
    {
        [ModTest("check/total_and_threshold")]
        static void TotalAndThreshold(ModTestContext ctx)
        {
            var check = new StatCheck { Stat = "cha", Difficulty = 12, Dice = "none", Bonus = 2 };

            check.Perform(10, null);
            ModAssert.Equal(12, check.Total, "10 + 0 + 2");
            ModAssert.True(check.Success, "reaching the difficulty exactly succeeds");

            check.Perform(9, null);
            ModAssert.False(check.Success, "one short fails");
            ModAssert.Equal(0, check.Roll, "no dice");
        }

        [ModTest("check/dice_add_to_the_total")]
        static void DiceAddToTheTotal(ModTestContext ctx)
        {
            var check = new StatCheck { Stat = "str", Difficulty = 100, Dice = "1d6", Bonus = -1 };
            var random = new Random(11);
            for (var i = 0; i < 50; i++)
            {
                check.Perform(5, random);
                ModAssert.True(check.Roll is >= 1 and <= 6, $"1d6 gave {check.Roll}");
                ModAssert.Equal(5 + check.Roll - 1, check.Total, "value + roll + bonus");
                ModAssert.False(check.Success, "100 is out of reach");
            }
        }
    }
}
