namespace Cthangover.Tests.Mods.Memory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Memory;
    using Xunit;

    /// <summary>Logical lookups stay measurable even while their data or an in-flight factory becomes stale.</summary>
    public sealed class DictionaryCacheTests : IDisposable
    {
        private readonly DictionaryCache<string, string> cache = new();
        private readonly ManualResetEventSlim started = new();
        private readonly ManualResetEventSlim resume = new();

        [Fact]
        public void MismatchedStampsCountAsMisses()
        {
            cache["source"] = "version-1";
            Assert.True(cache.TryGetValue("source", "version-1", Matches, out var valid));
            Assert.Equal("version-1", valid);
            Assert.False(cache.TryGetValue("source", "version-2", Matches, out var stale));
            Assert.Null(stale);
            Assert.False(cache.TryGetValue("missing", "version-1", Matches, out _));

            var metrics = cache.GetMetrics().Cache.Value;
            Assert.Equal(1L, metrics.Hits);
            Assert.Equal(2L, metrics.Misses);
        }

        [Fact]
        public async Task ClearPreventsAnInFlightFactoryFromRefillingTheCache()
        {
            var task = Task.Run(GetOldValue);
            try
            {
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                cache.Clear();
            }
            finally
            {
                resume.Set();
            }

            Assert.Equal("old", await task);
            Assert.Equal(0, cache.GetMetrics().Count);
            Assert.Equal("new", cache.GetOrAdd("key", BuildFresh));
            Assert.Equal("new", cache.GetOrAdd("key", BuildFresh));
            Assert.Equal(1L, cache.GetMetrics().Cache.Value.Hits);
            Assert.Equal(2L, cache.GetMetrics().Cache.Value.Misses);
        }

        public void Dispose()
        {
            started.Dispose();
            resume.Dispose();
        }

        private static bool Matches(string value, string stamp) => value == stamp;

        private string GetOldValue() => cache.GetOrAdd("key", BuildOld);

        private string BuildOld(string key)
        {
            started.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The cache factory was never released.");
            return "old";
        }

        private static string BuildFresh(string key) => "new";
    }
}
