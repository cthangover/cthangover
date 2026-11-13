namespace Cthangover.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Utils;

    /// <summary>Locks down texture key order, extension filtering and the distinct exact/prefix casing rules.</summary>
    public class TextureFileIndexTests
    {
        [Fact]
        public void FindsExactAndEveryDotPrefixWithoutMatchingNeighbourNames()
        {
            var index = new TextureFileIndex(new[] { "tree.txt", "tree.bare.PNG", "tree.png", "trees.png" }, Extensions());
            Assert.Equal("tree.bare.PNG", index.Find("tree"));
            Assert.Equal("tree.bare.PNG", index.Find("TREE.BARE"));
            Assert.Equal("tree.bare.PNG", index.Find("tree.bare.PNG"));
            Assert.Null(index.Find("tree.bare.png"));
            Assert.Equal("trees.png", index.Find("trees"));
            Assert.Null(index.Find("tre"));
            Assert.Null(index.Find(null));
        }

        [Fact]
        public void EarlierPrefixMatchBeatsLaterExactKeyAndNonTexturesNeverMatch()
        {
            var index = new TextureFileIndex(new[] { "tree.png.webp", "tree.png", "tree.gd" }, Extensions());
            Assert.Equal("tree.png.webp", index.Find("tree.png"));
            Assert.Null(index.Find("tree.gd"));
        }

        [Fact]
        public void RebuildingFromANewFileSnapshotSeesAddedRemovedAndOverlaidKeys()
        {
            var original = new TextureFileIndex(new[] { "old.png", "hero.jpg" }, Extensions());
            var refreshed = new TextureFileIndex(new[] { "new.png", "hero.webp" }, Extensions());
            Assert.Equal("old.png", original.Find("old"));
            Assert.Null(refreshed.Find("old"));
            Assert.Equal("new.png", refreshed.Find("new"));
            Assert.Equal("hero.webp", refreshed.Find("hero"));
        }

        private static HashSet<string> Extensions() => new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".webp" };
    }
}
