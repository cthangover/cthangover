namespace Cthangover.Tests.Mods.Compilation
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Compilation;

    /// <summary>Publication is immutable and competing compilers observe a complete pair after the lease is released.</summary>
    public sealed class AssemblyCacheEntryTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "assembly_publication_" + Guid.NewGuid().ToString("N"));
        private readonly ManualResetEventSlim acquired = new();
        private string Artifact => Path.Combine(directory, "probe_abc123.dll");

        [Fact]
        public void PublishesACompletePairAndNeverReplacesAnExistingPair()
        {
            using var lease = AssemblyCacheEntry.Acquire(Artifact);
            using var dll = new MemoryStream(new byte[] { 1, 2, 3 });
            using var pdb = new MemoryStream(new byte[] { 4, 5 });
            lease.Publish(dll, pdb);
            Assert.True(lease.HasArtifacts);
            using var other = new MemoryStream(new byte[] { 9 });
            lease.Publish(other, other);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Artifact));
            Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(Path.ChangeExtension(Artifact, ".pdb")));
            Assert.True(File.Exists(Artifact + ".used"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }

        [Fact]
        public async Task CompetingCompilerWaitsAndThenSeesThePublishedPair()
        {
            Task<bool> second;
            using (var first = AssemblyCacheEntry.Acquire(Artifact))
            {
                second = Task.Run(AcquireSecond);
                Assert.False(acquired.Wait(100));
                using var dll = new MemoryStream(new byte[] { 6 });
                using var pdb = new MemoryStream(new byte[] { 7 });
                first.Publish(dll, pdb);
            }
            Assert.True(await second);
            Assert.Equal(new byte[] { 6 }, File.ReadAllBytes(Artifact));
            Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.ChangeExtension(Artifact, ".pdb")));
        }

        private bool AcquireSecond()
        {
            using var lease = AssemblyCacheEntry.Acquire(Artifact);
            acquired.Set();
            return lease.HasArtifacts;
        }

        [Fact]
        public void LeaseTimeoutDoesNotDeleteACommittedPairOrItsLock()
        {
            using var first = AssemblyCacheEntry.Acquire(Artifact);
            using var bytes = new MemoryStream(new byte[] { 1 });
            first.Publish(bytes, bytes);
            Assert.Throws<TimeoutException>(AcquireWithShortTimeout);
            Assert.True(first.HasArtifacts);
            Assert.True(File.Exists(Artifact + ".lock"));
        }

        private void AcquireWithShortTimeout()
        {
            using var lease = AssemblyCacheEntry.Acquire(Artifact, timeout: TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void IncompleteDllIsRecoveredAndCancellationDoesNotAcquire()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Artifact, "interrupted");
            using var lease = AssemblyCacheEntry.Acquire(Artifact);
            Assert.False(lease.HasArtifacts);
            lease.RemoveIncomplete();
            Assert.False(File.Exists(Artifact));
            Assert.Throws<OperationCanceledException>(AcquireCancelled);
        }

        private void AcquireCancelled()
        {
            using var lease = AssemblyCacheEntry.Acquire(Artifact, new CancellationToken(true));
        }

        /// <summary>Removes test-owned artifacts and lease files after their handles close.</summary>
        public void Dispose()
        {
            acquired.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
