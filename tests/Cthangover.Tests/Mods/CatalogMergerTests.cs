using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the catalog merge rules: provenance tagging, cross-catalog
    /// deduplication and — most importantly — the hash rule. When listings of
    /// one id (compared case-insensitively) disagree on any content hash, every
    /// listing survives as a separate entry flagged unsafe, so the manager warns
    /// about all of them instead of silently merging (or picking) one.
    /// </summary>
    public class CatalogMergerTests
    {
        private static CatalogExportEntry Entry(
            string id, string version, string name, string installHash, string source)
        {
            return new CatalogExportEntry
            {
                Id = id,
                Name = name,
                Version = version,
                Downloads = new List<CatalogEntryDownload>
                {
                    new()
                    {
                        Version = version,
                        InstallSha256 = installHash,
                        Urls = new List<string> { $"https://{source}/{id}-{version}.zip" },
                    },
                },
                SourceUrls = new List<string> { source },
            };
        }

        [Fact]
        public void Merge_SameBuildFromTwoCatalogs_ProducesOneEntryWithBothSources()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_1", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Single(merged);
            Assert.Null(merged[0].VariantConflict);
            Assert.NotNull(merged[0].SourceUrls);
            Assert.Contains("cat_a", merged[0].SourceUrls);
            Assert.Contains("cat_b", merged[0].SourceUrls);
            Assert.Single(merged[0].Downloads);
        }

        [Fact]
        public void Merge_AgreeingCatalogsWithDifferentHistory_KeepsNewestAndAllDownloads()
        {
            var a = Entry("x", "1.1", "X", "hash_2", "cat_a");
            a.Downloads.Add(new CatalogEntryDownload { Version = "1.0", InstallSha256 = "hash_1", Urls = new List<string> { "https://cat_a/x-1.0.zip" } });
            var b = Entry("x", "1.1", "X", "hash_2", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Single(merged);
            Assert.Equal("1.1", merged[0].Version);
            Assert.Equal(2, merged[0].Downloads.Count);
            Assert.Null(merged[0].VariantConflict);
        }

        [Fact]
        public void Merge_DifferentCurrentVersionsAcrossCatalogs_MarksEveryListingUnsafe()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "99.0", "X", "hash_2", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Equal(2, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
            Assert.Contains(merged, e => e.Version == "1.0" && e.SourceUrls.Count == 1 && e.SourceUrls[0] == "cat_a");
            Assert.Contains(merged, e => e.Version == "99.0" && e.SourceUrls.Count == 1 && e.SourceUrls[0] == "cat_b");
        }

        [Fact]
        public void Merge_SharedOlderVersionWithAnotherHash_MarksEveryListingUnsafe()
        {
            var a = Entry("x", "2.0", "X", "hash_2", "cat_a");
            a.Downloads.Add(new CatalogEntryDownload { Version = "1.0", InstallSha256 = "hash_1" });
            var b = Entry("x", "2.0", "X", "hash_2", "cat_b");
            b.Downloads.Add(new CatalogEntryDownload { Version = "1.0", InstallSha256 = "forged" });

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Equal(2, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
        }

        [Fact]
        public void Merge_OneOfThreeCatalogsDiffers_MarksAllThreeUnsafe()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_1", "cat_b");
            var c = Entry("x", "1.0", "X", "hash_2", "cat_c");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b, c });

            Assert.Equal(3, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
            Assert.All(merged, e => Assert.Single(e.SourceUrls));
        }

        [Fact]
        public void Merge_MissingHashWithSeveralCatalogs_MarksEveryListingUnsafe()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", null, "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Equal(2, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
        }

        [Fact]
        public void Merge_SingleCatalogWithoutHash_IsNotFlagged()
        {
            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { Entry("x", "1.0", "X", null, "cat_a") });

            Assert.Single(merged);
            Assert.Null(merged[0].VariantConflict);
        }

        [Fact]
        public void Merge_IdsDifferingOnlyInCase_AreComparedAsOneMod()
        {
            var same = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry>
            {
                Entry("story", "1.0", "Story", "hash_1", "cat_a"), Entry("Story", "1.0", "Story", "hash_1", "cat_b"),
            });
            var forged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry>
            {
                Entry("story", "1.0", "Story", "hash_1", "cat_a"), Entry("STORY", "1.0", "Story", "hash_2", "cat_b"),
            });

            Assert.Single(same);
            Assert.Equal(2, same[0].SourceUrls.Count);
            Assert.Equal(2, forged.Count);
            Assert.All(forged, e => Assert.True(e.VariantConflict));
        }

        [Fact]
        public void Merge_SameVersionSameNameDifferentHash_SplitsIntoConflictingVariants()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_2", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Equal(2, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
            Assert.Single(merged[0].SourceUrls);
            Assert.Single(merged[1].SourceUrls);
            Assert.NotEqual(merged[0].SourceUrls[0], merged[1].SourceUrls[0]);
        }

        [Fact]
        public void Merge_SameVersionSameHashDifferentMirrors_UnionsDownloadUrls()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_1", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Single(merged);
            Assert.Equal(2, merged[0].Downloads[0].Urls.Count);
        }

        [Fact]
        public void Merge_SameVersionDifferentNameDifferentHash_MarksEveryListingUnsafe()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "Y", "hash_2", "cat_b");

            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            Assert.Equal(2, merged.Count);
            Assert.All(merged, e => Assert.True(e.VariantConflict));
        }

        [Fact]
        public void FindEntry_WithMatchingSource_ReturnsThatVariant()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_2", "cat_b");
            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            var found = CatalogMerger.FindEntry(merged, "x", "cat_b");

            Assert.NotNull(found);
            Assert.Contains("cat_b", found.SourceUrls);
        }

        [Fact]
        public void FindEntry_WithoutSource_ReturnsFirstMatchingEntry()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("y", "1.0", "Y", "hash_2", "cat_b");
            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            var found = CatalogMerger.FindEntry(merged, "y", null);

            Assert.NotNull(found);
            Assert.Equal("y", found.Id);
        }

        [Fact]
        public void FindEntry_UnknownSource_FallsBackToFirstMatch()
        {
            var a = Entry("x", "1.0", "X", "hash_1", "cat_a");
            var b = Entry("x", "1.0", "X", "hash_2", "cat_b");
            var merged = CatalogMerger.MergeAndDeduplicate(new List<CatalogExportEntry> { a, b });

            var found = CatalogMerger.FindEntry(merged, "x", "cat_unknown");

            Assert.NotNull(found);
            Assert.Equal("x", found.Id);
        }

        [Fact]
        public void TagSource_AddsUrlOnce()
        {
            var entry = new CatalogExportEntry { Id = "x" };

            CatalogMerger.TagSource(entry, "cat_a");
            CatalogMerger.TagSource(entry, "cat_a");
            CatalogMerger.TagSource(entry, "cat_b");

            Assert.Equal(2, entry.SourceUrls.Count);
            Assert.Contains("cat_a", entry.SourceUrls);
            Assert.Contains("cat_b", entry.SourceUrls);
        }
    }
}
