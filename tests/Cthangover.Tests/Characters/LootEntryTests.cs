using System;
using Cthangover.Core.Characters;

namespace Engine.Tests.Characters
{
    /// <summary>
    /// A loot roll never throws on authoring slips in the loot table (swapped
    /// or negative counts) — the victory screen rolls every entry of every
    /// defeated enemy and a throw there stopped the whole battle end.
    /// </summary>
    public class LootEntryTests
    {
        [Fact]
        public void Roll_SwappedBounds_StaysInsideThem()
        {
            var entry = new LootEntry { ItemId = "x", MinCount = 3, MaxCount = 1, Probability = 100 };
            var random = new Random(7);

            for (var i = 0; i < 50; i++)
                Assert.InRange(entry.Roll(random), 1, 3);
        }

        [Fact]
        public void Roll_ProbabilityBounds_DecideAlwaysOrNever()
        {
            var random = new Random(7);
            var always = new LootEntry { ItemId = "x", MinCount = 2, MaxCount = 2, Probability = 150 };
            var never = new LootEntry { ItemId = "x", MinCount = 2, MaxCount = 2, Probability = 0 };

            for (var i = 0; i < 50; i++)
            {
                Assert.Equal(2, always.Roll(random));
                Assert.Equal(0, never.Roll(random));
            }
        }

        [Fact]
        public void Roll_NegativeCounts_GiveNothing()
        {
            var entry = new LootEntry { ItemId = "x", MinCount = -4, MaxCount = -1, Probability = 100 };

            Assert.Equal(0, entry.Roll(new Random(7)));
        }
    }
}
