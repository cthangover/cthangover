namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using Cthangover.TestRunner;

    /// <summary>Expiration uses last consumption, skips active leases and leaves persistent data outside disposable runs.</summary>
    public sealed class CompilationCachePolicyTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "cache_policy_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void PersistentPathIsStableAndFreshPathIsOwnedByItsRun()
        {
            Assert.Equal(CompilationCachePolicy.Resolve(directory, "one", false), CompilationCachePolicy.Resolve(directory, "two", false));
            Assert.Equal(Path.Combine(directory, "cthangover-tests", "cache", "assemblies"), CompilationCachePolicy.Resolve(directory, "one", false));
            Assert.Equal(Path.Combine(directory, "cthangover-tests", "one", "cache", "assemblies"), CompilationCachePolicy.Resolve(directory, "one", true));
            Assert.True(RunnerOptions.Parse(new[] { "--fresh-cache" }).FreshCache);
        }

        [Fact]
        public void PrunesOldPairsButRetainsRecentlyUsedAndLeasedEntries()
        {
            var assemblies = Path.Combine(directory, "assemblies");
            var tests = Path.Combine(directory, "test_assemblies");
            Directory.CreateDirectory(assemblies);
            Directory.CreateDirectory(tests);
            var old = CreatePair(assemblies, "old");
            var recent = CreatePair(assemblies, "recent");
            var locked = CreatePair(assemblies, "locked");
            var test = CreatePair(tests, "old_test");
            File.WriteAllText(recent + ".used", "");
            using var lease = new FileStream(locked + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(2, CompilationCachePolicy.Prune(assemblies, DateTime.UtcNow));
            Assert.False(File.Exists(old));
            Assert.False(File.Exists(Path.ChangeExtension(old, ".pdb")));
            Assert.False(File.Exists(test));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(locked));
        }

        private static string CreatePair(string root, string name)
        {
            var path = Path.Combine(root, name + "_abc.dll");
            File.WriteAllText(path, "dll");
            File.WriteAllText(Path.ChangeExtension(path, ".pdb"), "pdb");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-15));
            return path;
        }

        /// <summary>Removes only this test's cache fixture.</summary>
        public void Dispose()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
