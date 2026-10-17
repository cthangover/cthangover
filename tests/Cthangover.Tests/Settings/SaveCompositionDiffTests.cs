using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    /// <summary>The load-time comparison tells the player what a composition change does to the save.</summary>
    public class SaveCompositionDiffTests
    {
        [Fact]
        public void ReportsAddedRemovedAndUpdated()
        {
            var saved = new[] { Mod("story", "1.0.0"), Mod("recruiting", "1.1.0"), Mod("cards", "1.0.0") };
            var current = new[] { Mod("story", "1.0.0"), Mod("cards", "1.2.0"), Mod("cooking", "1.0.0") };

            var diff = SaveCompositionDiff.Compute(saved, current);

            Assert.Equal(new[] { "cooking" }, diff.Added);
            Assert.Equal(new[] { "recruiting" }, diff.Removed);
            var update = Assert.Single(diff.Updated);
            Assert.Equal(("cards", "1.0.0", "1.2.0", false), update);
        }

        [Fact]
        public void FlagsDowngrade()
        {
            var diff = SaveCompositionDiff.Compute(new[] { Mod("cards", "2.0.0") }, new[] { Mod("cards", "1.0.0") });

            Assert.True(Assert.Single(diff.Updated).Downgrade);
        }

        [Fact]
        public void SameComposition_IsEmpty_AndSaveWithoutSnapshot_IsEmpty()
        {
            Assert.True(SaveCompositionDiff.Compute(new[] { Mod("a", "1") }, new[] { Mod("a", "1") }).IsEmpty);
            Assert.True(SaveCompositionDiff.Compute(null, new[] { Mod("a", "1") }).IsEmpty);
        }

        private static SaveGameMod Mod(string id, string version) => new() { Id = id, Version = version };
    }
}
