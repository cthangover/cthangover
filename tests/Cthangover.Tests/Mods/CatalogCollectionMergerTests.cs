using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the online collection merge rules: catalogs publishing the same
    /// collection id collapse into the newest version, and every publisher is
    /// recorded so the UI can show provenance.
    /// </summary>
    public class CatalogCollectionMergerTests
    {
        [Fact]
        public void TagSource_AddsUrlOnce()
        {
            var collection = new ModCollection { Id = "x" };

            CatalogCollectionMerger.TagSource(collection, "https://a/catalog.json");
            CatalogCollectionMerger.TagSource(collection, "https://a/catalog.json");
            CatalogCollectionMerger.TagSource(collection, "https://b/catalog.json");

            Assert.Equal(2, collection.SourceUrls.Count);
            Assert.Equal("https://a/catalog.json", collection.SourceUrls[0]);
            Assert.Equal("https://b/catalog.json", collection.SourceUrls[1]);
        }

        [Fact]
        public void Merge_SameId_KeepsNewestVersionAndMergesSources()
        {
            var older = new ModCollection
            {
                Id = "card_novel",
                Version = "1.0.0",
                SourceUrls = new List<string> { "https://a/catalog.json" },
            };
            var newer = new ModCollection
            {
                Id = "card_novel",
                Version = "2.0.0",
                SourceUrls = new List<string> { "https://b/catalog.json" },
            };

            var merged = CatalogCollectionMerger.MergeAndDeduplicate(new List<ModCollection> { older, newer });

            var collection = Assert.Single(merged);
            Assert.Equal("2.0.0", collection.Version);
            Assert.Equal(2, collection.SourceUrls.Count);
            Assert.Contains("https://a/catalog.json", collection.SourceUrls);
            Assert.Contains("https://b/catalog.json", collection.SourceUrls);
        }

        [Fact]
        public void Merge_NewestFirst_StillKeepsNewest()
        {
            var newer = new ModCollection { Id = "x", Version = "3.0" };
            var older = new ModCollection { Id = "x", Version = "1.0" };

            var merged = CatalogCollectionMerger.MergeAndDeduplicate(new List<ModCollection> { newer, older });

            var collection = Assert.Single(merged);
            Assert.Equal("3.0", collection.Version);
        }

        [Fact]
        public void Merge_DifferentIds_AllKept()
        {
            var a = new ModCollection { Id = "a", Version = "1.0" };
            var b = new ModCollection { Id = "b", Version = "1.0" };

            var merged = CatalogCollectionMerger.MergeAndDeduplicate(new List<ModCollection> { a, b });

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void Merge_NullEmptyAndIdlessEntries_AreIgnored()
        {
            var merged = CatalogCollectionMerger.MergeAndDeduplicate(new List<ModCollection>
            {
                null,
                new ModCollection { Name = "no id" },
                new ModCollection { Id = "", Version = "1.0" },
                new ModCollection { Id = "valid", Version = "1.0" },
            });

            var collection = Assert.Single(merged);
            Assert.Equal("valid", collection.Id);
        }

        [Fact]
        public void Merge_NullInput_ReturnsEmpty()
        {
            Assert.Empty(CatalogCollectionMerger.MergeAndDeduplicate(null));
        }
    }
}
