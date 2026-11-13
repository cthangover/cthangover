namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Compilation;

    /// <summary>Shared library paths represent complete immutable bytes, including concurrent cold publication and corruption failures.</summary>
    [Collection("runner-infrastructure")]
    public class PrecompiledAssemblyCacheTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "precompiled_cache_" + Guid.NewGuid().ToString("N"));
        private readonly byte[] bytes = Enumerable.Repeat((byte)51, 256 * 1024).ToArray();
        private readonly ManualResetEventSlim start = new();

        [Fact]
        public void Net10StagingDoesNotReuseTheLegacyNet8CachePath()
        {
            Directory.CreateDirectory(directory);
            var legacyKey = Convert.ToHexString(SHA256.HashData(bytes)).Substring(0, 16).ToLowerInvariant();
            var legacy = Path.Combine(directory, "probe_dll_" + legacyKey + "_library.dll");
            File.WriteAllBytes(legacy, new byte[] { 8 });

            var current = Stage();

            Assert.NotEqual(legacy, current);
            Assert.Equal(bytes, File.ReadAllBytes(current));
            Assert.Equal(new byte[] { 8 }, File.ReadAllBytes(legacy));
        }

        [Fact]
        public void RepeatedStagingKeepsTheCompleteLibraryAndItsTimestamp()
        {
            var path = Stage();
            var timestamp = File.GetLastWriteTimeUtc(path);
            Assert.Equal(path, Stage());
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
            Assert.True(File.Exists(path + ".used"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }

        [Fact]
        public void ChangedLibraryGetsANewPathWithoutReplacingTheMappedFile()
        {
            var path = Stage();
            using var input = File.OpenRead(path);
            var changed = bytes.ToArray();
            changed[0] = 52;
            var other = PrecompiledAssemblyCache.Stage(directory, "probe", "dll/library.dll", changed);
            Assert.NotEqual(path, other);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(changed, File.ReadAllBytes(other));
        }

        [Fact]
        public async Task ConcurrentPublishersReturnOneCompleteLibrary()
        {
            var first = Task.Factory.StartNew(WaitAndStage, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var second = Task.Factory.StartNew(WaitAndStage, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var third = Task.Factory.StartNew(WaitAndStage, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            start.Set();
            var paths = await Task.WhenAll(first, second, third);
            Assert.Equal(paths[0], paths[1]);
            Assert.Equal(paths[0], paths[2]);
            Assert.Single(Directory.GetFiles(directory, "*.dll"));
            Assert.Equal(bytes, File.ReadAllBytes(paths[0]));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }

        [Fact]
        public async Task ColdPublisherWaitsForTheCrossProcessLease()
        {
            var path = Stage();
            File.Delete(path);
            Task<string> pending;
            using (AssemblyCacheEntry.Acquire(path))
            {
                pending = Task.Factory.StartNew(WaitAndStage, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                start.Set();
                var delay = Task.Delay(100);
                Assert.Same(delay, await Task.WhenAny(pending, delay));
                Assert.False(File.Exists(path));
            }
            Assert.Equal(path, await pending);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        [Fact]
        public void CorruptionFailsWithoutOverwritingAnExistingArtifact()
        {
            var path = Stage();
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.Throws<IOException>(Stage);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        }

        [Fact]
        public void CancellationDoesNotPublishADll()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => PrecompiledAssemblyCache.Stage(directory, "probe", "library.dll", bytes, cancellation.Token));
            Assert.Empty(Directory.GetFiles(directory, "*.dll"));
        }

        private string Stage() => PrecompiledAssemblyCache.Stage(directory, "probe", "dll/library.dll", bytes);
        private string WaitAndStage() { start.Wait(); return Stage(); }

        public void Dispose()
        {
            start.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
