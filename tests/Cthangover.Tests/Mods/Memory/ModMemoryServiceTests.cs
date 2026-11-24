namespace Cthangover.Tests.Mods.Memory
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Loader;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Memory;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Tests.Mods.Unload;
    using Xunit;

    /// <summary>Exercises registered stores through the same owner lookup and unload path available to mods.</summary>
    [Collection("mod-load-context")]
    public class ModMemoryServiceTests : IDisposable
    {
        private const string MarkerSource = "public sealed class MemoryTestMarker { }";
        private static ModMemoryService Service => ModMemoryService.Shared;
        private string archivePath;

        public void Dispose()
        {
            if (archivePath != null)
                File.Delete(archivePath);
        }

        [Fact]
        public void QueriesReportLiveMetricsAndPageOnlyExplicitPreviews()
        {
            var id = TestMods.NewId("test_memory");
            var assembly = TestMods.Compile(id, MarkerSource);
            try
            {
                var scope = Service.For(assembly);
                var cache = scope.CreateCache<string, string>("cache", StringComparer.OrdinalIgnoreCase, CachePreview);
                Assert.False(cache.TryGetValue("entry", out _));
                Assert.True(cache.TryAdd("Entry", new string('a', 300)));
                Assert.True(cache.TryGetValue("entry", out var value));
                Assert.Equal(300, value.Length);

                var list = scope.CreateList<string>("list", ListPreview);
                list.Add("one");
                list.Add("two");
                list.Add("three");

                var set = scope.CreateHashSet<string>("set", StringComparer.OrdinalIgnoreCase);
                Assert.True(set.Add("alpha"));
                Assert.False(set.Add("ALPHA"));

                var dictionary = scope.CreateDictionary<string, int>("data");
                dictionary["answer"] = 42;
                Assert.Equal(42, dictionary["answer"]);
                Assert.Throws<ArgumentException>(() => scope.Track("list", dictionary));

                var (firstPage, total) = Service.GetStatistics(id.ToUpperInvariant(), limit: 1);
                Assert.Equal(4, total);
                var row = Assert.Single(firstPage);
                Assert.Equal("cache", row.Name);
                Assert.Equal(scope.Generation, row.Generation);
                Assert.True(row.IsCache);
                Assert.Equal(1, row.Metrics.Value.Count);
                Assert.True(row.Metrics.Value.Capacity >= 1);
                Assert.Null(row.Metrics.Value.EstimatedPayloadBytes);
                Assert.Equal(1L, row.Metrics.Value.Cache.Value.Hits);
                Assert.Equal(1L, row.Metrics.Value.Cache.Value.Misses);

                var preview = Service.Inspect(id, "cache", generation: scope.Generation);
                var entry = Assert.Single(preview.Entries);
                Assert.Equal("Entry", entry.Key);
                Assert.Equal(256, entry.Value.Length);
                Assert.EndsWith("…", entry.Value);
                Assert.Null(preview.NextOffset);

                var listPage = Service.Inspect(id, "list", offset: 1, limit: 1);
                Assert.Equal("two", Assert.Single(listPage.Entries).Value);
                Assert.Equal(2, listPage.NextOffset);
                Assert.Null(Service.Inspect(id, "set").Entries);
                Assert.Empty(Service.Inspect(id, "list", offset: 3).Entries);
                Assert.Throws<ArgumentOutOfRangeException>(() => Service.Inspect(id, "list", limit: 101));

                cache.Clear();
                var cleared = Service.Inspect(id, "cache").Storage.Value.Metrics.Value;
                Assert.Equal(0, cleared.Count);
                Assert.Equal(row.Metrics.Value.Capacity, cleared.Capacity);
                Assert.Equal(1L, cleared.Cache.Value.Hits);
                Assert.True(scope.Untrack("data", dictionary));
                Assert.Null(Service.Inspect(id, "data").Storage);

                GC.KeepAlive(list);
                GC.KeepAlive(set);
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(id);
            }
        }

        [Fact]
        public void DetachedSnapshotSurvivesUnloadWithANewGenerationForTheSameMod()
        {
            var id = TestMods.NewId("test_memory_reload");
            var released = TakeSnapshotAndUnload(id);
            Assert.Empty(Service.GetStatistics(id).Storages);
            Assert.Null(ModLoadContext.CollectAndVerify(released.Context, id));
            Assert.Equal("assembly", Assert.Single(released.Entries).Key);

            var assembly = TestMods.Compile(id, MarkerSource);
            try
            {
                var scope = Service.For(assembly);
                var replacement = scope.CreateList<string>("assemblies");
                Assert.True(scope.Generation > released.Generation);
                Assert.Null(Service.Inspect(id, "assemblies", generation: released.Generation).Storage);
                Assert.Equal(scope.Generation, Service.Inspect(id, "assemblies").Storage.Value.Generation);
                GC.KeepAlive(replacement);
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(id);
            }
        }

        [Fact]
        public void RegistrationsDoNotKeepUnownedStoresAlive()
        {
            var id = TestMods.NewId("test_memory_weak");
            var assembly = TestMods.Compile(id, MarkerSource);
            try
            {
                var scope = Service.For(assembly);
                var store = TrackTemporaryStore(scope);

                Assert.Null(ModLoadContext.CollectAndVerify(store, id));
                Assert.Empty(Service.GetStatistics(id).Storages);
                var replacement = scope.CreateList<string>("temporary");
                Assert.Equal(1, Service.GetStatistics(id).Total);
                GC.KeepAlive(replacement);
            }
            finally
            {
                ModAssemblyLoader.UnloadMod(id);
            }
        }

        [Fact]
        public void ArchiveMetricsReflectBufferedBytesAndProviderDisposal()
        {
            const string name = "mods.archive_content";
            const string path = "Data/payload.bin";
            byte[] payload = [0, 1, 2, 3];
            archivePath = Path.Combine(Path.GetTempPath(), "cthangover-memory-archive-" + Guid.NewGuid().ToString("N") + ".zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            using (var entry = archive.CreateEntry(path).Open())
                entry.Write(payload);

            var baseline = Service.Inspect(ModMemoryService.CoreOwner, name).Storage?.Metrics;
            using (var provider = new ZipModFileProvider(archivePath, "test_memory_archive"))
            {
                var bytes = provider.ReadFileBinary(path);
                Assert.Equal(payload, bytes);
                using var stream = provider.OpenStream(path);
                Assert.Equal(0, stream.ReadByte());

                var result = Service.Inspect(ModMemoryService.CoreOwner, name);
                var metrics = result.Storage.Value.Metrics.Value;
                Assert.Equal((baseline?.Count ?? 0) + 1, metrics.Count);
                Assert.Equal((baseline?.EstimatedPayloadBytes ?? 0) + bytes.Length, metrics.EstimatedPayloadBytes);
                Assert.Equal((baseline?.Cache?.Hits ?? 0) + 1, metrics.Cache.Value.Hits);
                Assert.Equal(16L * 1024 * 1024, metrics.Cache.Value.BudgetBytes);
                Assert.Null(result.Entries);
            }

            var released = Service.Inspect(ModMemoryService.CoreOwner, name).Storage.Value.Metrics.Value;
            Assert.Equal(baseline?.Count ?? 0, released.Count);
            Assert.Equal(baseline?.EstimatedPayloadBytes ?? 0, released.EstimatedPayloadBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Context, long Generation, IReadOnlyList<MemoryPreviewEntry> Entries) TakeSnapshotAndUnload(string id)
        {
            var assembly = TestMods.Compile(id, MarkerSource);
            var context = (ModLoadContext)AssemblyLoadContext.GetLoadContext(assembly);
            var scope = Service.For(assembly);
            var cache = scope.CreateCache<string, Assembly>("assemblies", preview: AssemblyPreview);
            cache["assembly"] = assembly;
            var snapshot = Service.Inspect(id, "assemblies");
            Assert.NotNull(snapshot.Storage);
            GC.KeepAlive(cache);

            var weak = new WeakReference(context);
            // Detach from the loader and get out of the frame before verifying collection.
            ModAssemblyLoader.UnloadMod(id);
            Assert.Throws<ObjectDisposedException>(() => scope.CreateList<string>("late"));
            return (weak, scope.Generation, snapshot.Entries);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference TrackTemporaryStore(ModMemoryScope scope)
            => new(scope.CreateList<string>("temporary"));

        private static MemoryPreviewEntry CachePreview(string key, string value) => new(key, value);

        private static MemoryPreviewEntry ListPreview(string value) => new(null, value);

        private static MemoryPreviewEntry AssemblyPreview(string key, Assembly assembly) => new(key, assembly.GetName().Name);
    }
}
