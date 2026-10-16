using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    /// <summary>
    /// Контракты LRU-кэша с callback'ом вытеснения: вытеснение по ёмкости,
    /// порядок LRU, сброс через Invalidate/Clear и поведение на null-значениях
    /// загрузчика. Проверяются данные (что именно и сколько раз вытеснено),
    /// а не факты внутренних вызовов.
    /// </summary>
    public class BoundedCacheTests
    {
        /// <summary>Loader, возвращающий для каждого ключа значение "v-ключ" — удобно проверять, какой именно payload вытеснен.</summary>
        private sealed class KeyedLoader : ICacheLoader<string, string>
        {
            public int LoadCalls;

            public string Load(string key)
            {
                LoadCalls++;
                return "v-" + key;
            }
        }

        /// <summary>Loader с настраиваемым значением на каждый промах; null означает «не кэшировать».</summary>
        private sealed class FixedLoader : ICacheLoader<string, string>
        {
            public int LoadCalls;
            public string NextValue;

            public string Load(string key)
            {
                LoadCalls++;
                return NextValue;
            }
        }

        [Fact]
        public void Get_Miss_LoadsThroughLoader()
        {
            var loader = new KeyedLoader();
            var cache = new BoundedCache<string, string>(2, loader);

            Assert.Equal("v-a", cache.Get("a"));
            Assert.Equal(1, loader.LoadCalls);
        }

        [Fact]
        public void Get_Hit_DoesNotReload()
        {
            var loader = new KeyedLoader();
            var cache = new BoundedCache<string, string>(2, loader);

            cache.Get("a");
            Assert.Equal("v-a", cache.Get("a"));
            Assert.Equal(1, loader.LoadCalls);
        }

        [Fact]
        public void Get_CapacityExceeded_EvictsLeastRecentlyUsedWithCallback()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(2, loader, evicted.Add);

            cache.Get("a");
            cache.Get("b");
            cache.Get("c");

            Assert.Equal(2, cache.Count);
            Assert.Equal(new[] { "v-a" }, evicted);
            Assert.Equal("v-b", cache.Get("b"));
            Assert.Equal("v-c", cache.Get("c"));
            Assert.Equal(3, loader.LoadCalls);
        }

        [Fact]
        public void Get_TouchRefreshesLruOrder_EvictsNotRecentlyUsed()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(2, loader, evicted.Add);

            cache.Get("a");
            cache.Get("b");
            cache.Get("a");
            cache.Get("c");

            Assert.Equal(2, cache.Count);
            Assert.Equal(new[] { "v-b" }, evicted);
            Assert.Equal("v-a", cache.Get("a"));
            Assert.Equal(3, loader.LoadCalls);
        }

        [Fact]
        public void Get_LoaderReturnsNull_NotCachedAndNoEviction()
        {
            var loader = new FixedLoader { NextValue = null };
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(2, loader, evicted.Add);

            Assert.Null(cache.Get("a"));
            Assert.Equal(0, cache.Count);
            Assert.Null(cache.Get("a"));
            Assert.Equal(2, loader.LoadCalls);
            Assert.Empty(evicted);
        }

        [Fact]
        public void Invalidate_ExistingKey_NotifiesCallbackAndReloads()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(2, loader, evicted.Add);

            cache.Get("a");
            cache.Invalidate("a");

            Assert.Equal(0, cache.Count);
            Assert.Equal(new[] { "v-a" }, evicted);
            Assert.Equal("v-a", cache.Get("a"));
            Assert.Equal(2, loader.LoadCalls);
        }

        [Fact]
        public void Invalidate_MissingKey_DoesNotNotify()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(2, loader, evicted.Add);

            cache.Get("a");
            cache.Invalidate("missing");

            Assert.Empty(evicted);
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void Clear_NotifiesForEveryEntry()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(0, loader, evicted.Add);

            cache.Get("a");
            cache.Get("b");
            cache.Get("c");
            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.Equal(3, evicted.Count);
            Assert.Contains("v-a", evicted);
            Assert.Contains("v-b", evicted);
            Assert.Contains("v-c", evicted);
        }

        [Fact]
        public void EvictionWithoutCallback_DoesNotThrow()
        {
            var cache = new BoundedCache<string, string>(2, new KeyedLoader());

            cache.Get("a");
            cache.Get("b");
            cache.Get("c");

            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void MaxSize_Zero_UnboundedAndNoEviction()
        {
            var loader = new KeyedLoader();
            var evicted = new List<string>();
            var cache = new BoundedCache<string, string>(0, loader, evicted.Add);

            cache.Get("a");
            cache.Get("b");
            cache.Get("c");

            Assert.Equal(0, cache.MaxSize);
            Assert.Equal(3, cache.Count);
            Assert.Empty(evicted);
        }
    }
}
