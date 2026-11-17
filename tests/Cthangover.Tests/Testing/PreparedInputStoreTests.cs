namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.TestRunner;

    /// <summary>Checks physical input bytes, concurrent ownership and recovery at deterministic preparation boundaries.</summary>
    [Collection("runner-infrastructure")]
    public sealed class PreparedInputStoreTests : IDisposable
    {
        private readonly PreparedInputFixture fixture = new();
        private readonly CancellationTokenSource cancellation = new();
        private PreparationGate gate;
        private Task<PreparedInputLease> preparation;

        /// <summary>A single budget bounds actual work and partial deletion cannot be admitted or silently repaired.</summary>
        [Fact]
        public void BoundedRetentionDefersPartialWorkAndResumesWithoutAdmittingRetiringInputs()
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.Work.MaxWorkUnits = 512;
            var decision = Assert.Single(retention.Prune());
            Assert.Equal("deferred", decision.Decision);
            Assert.Equal("work-budget", decision.Reason);
            Assert.True(decision.Partial);
            Assert.InRange(decision.WorkUnits, 1, 512);
            Assert.True(decision.FilesRemoved > 0);
            Assert.True(decision.BytesRemoved > 0);
            Assert.True(decision.EntriesVisited > 0);
            Assert.True(decision.AttributeReads > 0);
            Assert.True(Directory.Exists(retention.PreparedRoot));
            Assert.Equal("prepared-input-retiring", Assert.Throws<PreparedInputRefusal>(retention.AcquireAndRelease).Reason);
            var repair = Assert.Throws<PreparedInputRefusal>(retention.RepairAndRelease);
            Assert.Equal("prepared-input-retiring", repair.Reason);
            AssertRetentionRoundTrip(decision);
            retention.Work.MaxWorkUnits = 100000;
            Assert.Equal("removed", Assert.Single(retention.Prune()).Decision);
            using var recovered = retention.Acquire();
            Assert.Empty(recovered.VerifyUnchanged());
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(retention.Source, "mods", "probe", "data.json")));
        }

        /// <summary>No tree operation is permitted after an explicit zero work or time boundary.</summary>
        [Theory]
        [InlineData(0, 10, "work-budget")]
        [InlineData(100000, 0, "time-budget")]
        public void BoundedRetentionZeroBudgetDoesNotDeleteInputs(int workUnits, double seconds, string reason)
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.Work.MaxWorkUnits = workUnits;
            retention.Work.MaxSeconds = seconds;
            var decision = Assert.Single(retention.Prune());
            Assert.Equal("deferred", decision.Decision);
            Assert.Equal(reason, decision.Reason);
            Assert.Equal(0, decision.WorkUnits);
            Assert.Equal(0, decision.FilesRemoved);
            Assert.Equal(0, decision.DirectoriesRemoved);
            Assert.False(decision.Partial);
            using var admitted = retention.Acquire();
            Assert.Empty(admitted.VerifyUnchanged());
        }

        /// <summary>Cancellation after one observed deletion preserves quarantine and the next bounded call can finish cleanup.</summary>
        [Fact]
        public void BoundedRetentionCancellationPreservesRetiringAdmissionBarrier()
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.CancelAfterDeletion = true;
            Assert.ThrowsAny<OperationCanceledException>(retention.PruneAndDiscard);
            Assert.Contains(retention.Observed, HasDeletedFile);
            Assert.Equal("prepared-input-retiring", Assert.Throws<PreparedInputRefusal>(retention.AcquireAndRelease).Reason);
            Assert.Equal("prepared-input-retiring", Assert.Throws<PreparedInputRefusal>(retention.RepairAndRelease).Reason);
            var resumed = PreparedInputStore.Prune(retention.Cache, 0, 0, 0);
            Assert.Equal("removed", Assert.Single(resumed).Decision);
            using var recovered = retention.Acquire();
            Assert.Empty(recovered.VerifyUnchanged());
        }

        /// <summary>An already cancelled call cannot turn sealed inputs into a retiring generation.</summary>
        [Fact]
        public void BoundedRetentionPreCancellationLeavesSealedInputsAdmissible()
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(retention.PruneAndDiscard);
            Assert.Empty(retention.Observed);
            using var admitted = retention.Acquire();
            Assert.Empty(admitted.VerifyUnchanged());
        }

        /// <summary>
        /// Real held kernel leases protect inputs before a payload walk; an unrelated unmarked candidate stays byte-identical.
        /// </summary>
        [Fact]
        public void BoundedRetentionProtectsActiveLeaseAndRefusesUnownedCandidate()
        {
            using var retention = new RetentionFixture();
            using var active = retention.Acquire();
            var unknown = Path.Combine(Path.GetDirectoryName(active.Root), "..", "unowned");
            Directory.CreateDirectory(unknown);
            var sentinel = Path.Combine(unknown, "sentinel.txt");
            File.WriteAllText(sentinel, "external ownership sentinel");
            var decisions = retention.Prune();
            Assert.Contains(decisions, IsActiveRetention);
            Assert.Contains(decisions, IsUnownedRetention);
            Assert.Equal("external ownership sentinel", File.ReadAllText(sentinel));
            Assert.Empty(active.VerifyUnchanged());
            using var reader = retention.Acquire();
            Assert.Equal(active.PreparedInputId, reader.PreparedInputId);
            Assert.NotEqual(active.LeaseId, reader.LeaseId);
        }

        /// <summary>Incomplete size measurement never authorizes eviction or claims completed retention.</summary>
        [Fact]
        public void BoundedRetentionSizeMeasurementBudgetNeverDeletesUnmeasuredStore()
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.MaxCount = int.MaxValue;
            retention.MaxDays = 365;
            retention.MaxBytes = 1;
            retention.Work.MaxWorkUnits = 128;
            var before = retention.StoreInventory();
            var inactive = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var pair in before)
            {
                if (pair.Key.StartsWith("leases/", StringComparison.Ordinal) && pair.Key.EndsWith(".json", StringComparison.Ordinal))
                {
                    var owner = JsonSerializer.Deserialize<PreparedInputOwner>(pair.Value,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    Assert.False(owner.Active);
                    Assert.Equal(1, owner.SchemaVersion);
                    Assert.Equal(retention.PreparedRoot, owner.Root);
                    Assert.Equal(Path.GetFileNameWithoutExtension(pair.Key), owner.LeaseId);
                    inactive.Add(pair.Key, pair.Value);
                }
            }

            Assert.NotEmpty(inactive);
            var decision = Assert.Single(retention.Prune());
            var after = retention.StoreInventory();
            retention.RecordEvidence(new Dictionary<string, object>
            {
                ["decision"] = decision,
                ["inactiveLeaseFilesBefore"] = inactive,
                ["storeBefore"] = before,
                ["storeAfter"] = after
            });
            Assert.Equal("deferred", decision.Decision);
            Assert.Equal("measurement-budget", decision.Reason);
            Assert.InRange(decision.WorkUnits, 1, 128);
            long removedFiles = 0;
            long removedBytes = 0;
            foreach (var pair in before)
            {
                if (after.TryGetValue(pair.Key, out var bytes))
                {
                    Assert.Equal(pair.Value, bytes);
                }
                else
                {
                    Assert.True(inactive.ContainsKey(pair.Key), "Deletion outside captured inactive lease records: " + pair.Key);
                    removedFiles++;
                    removedBytes += pair.Value.LongLength;
                }
            }

            foreach (var path in after.Keys)
            {
                Assert.True(before.ContainsKey(path), "Unexpected new store metadata/payload: " + path);
            }

            Assert.InRange(removedFiles, 1, inactive.Count);
            Assert.Equal(removedFiles, decision.FilesRemoved);
            Assert.Equal(removedFiles, decision.ControlRecordsRemoved);
            Assert.Equal(removedBytes, decision.BytesRemoved);
            Assert.Equal(0, decision.DirectoriesRemoved);
            Assert.False(decision.Partial);
            var root = retention.PreparedRoot;
            using var admitted = retention.Acquire();
            Assert.Equal(root, admitted.Root);
            Assert.True(admitted.Metrics.CacheHit);
            Assert.Equal(0, admitted.Metrics.FilesCopied);
            Assert.Equal(0, admitted.Metrics.BytesCopied);
            Assert.Equal(0, admitted.Metrics.FullProjectCopies);
            Assert.Empty(admitted.VerifyUnchanged());
        }

        /// <summary>Historical inactive lease records and a second store consume the same whole-call control/payload budget.</summary>
        [Fact]
        public void BoundedRetentionCountsControlTraversalAcrossMultipleStores()
        {
            using var retention = new RetentionFixture(4);
            for (var index = 0; index < 256; index++)
            {
                retention.Prepare();
            }

            retention.PreparePeer();
            retention.Work.MaxWorkUnits = 512;
            var removed = new HashSet<string>(StringComparer.Ordinal);
            long controlsRemoved = 0;
            var deferred = false;
            for (var pass = 0; pass < 16 && removed.Count < 2; pass++)
            {
                var decisions = retention.Prune();
                Assert.NotEmpty(decisions);
                long workUnits = 0;
                foreach (var decision in decisions)
                {
                    workUnits += decision.WorkUnits;
                    controlsRemoved += decision.ControlRecordsRemoved;
                    deferred |= decision.Decision == "deferred" && decision.Reason == "work-budget";
                    if (decision.Decision == "removed")
                    {
                        removed.Add(decision.Path);
                    }

                    AssertRetentionRoundTrip(decision);
                }

                Assert.InRange(workUnits, 1, 512);
            }

            Assert.True(deferred);
            Assert.Equal(2, removed.Count);
            Assert.True(controlsRemoved >= 256);
            using var admitted = retention.Acquire();
            Assert.Empty(admitted.VerifyUnchanged());
        }

        /// <summary>Invalid work/time boundaries are rejected instead of becoming unlimited cleanup.</summary>
        [Theory]
        [InlineData(-1, 10)]
        [InlineData(1, -1)]
        [InlineData(1, double.NaN)]
        [InlineData(1, double.PositiveInfinity)]
        public void BoundedRetentionRejectsInvalidLimits(int workUnits, double seconds)
        {
            using var retention = new RetentionFixture();
            retention.Prepare();
            retention.Work.MaxWorkUnits = workUnits;
            retention.Work.MaxSeconds = seconds;
            Assert.Throws<ArgumentOutOfRangeException>(retention.PruneAndDiscard);
            using var admitted = retention.Acquire();
            Assert.Empty(admitted.VerifyUnchanged());
        }

        private static bool HasDeletedFile(InputRetentionDecision decision)
        {
            return decision.FilesRemoved > 0;
        }

        private static bool IsActiveRetention(InputRetentionDecision decision)
        {
            return decision.Decision == "retained-active";
        }

        private static bool IsUnownedRetention(InputRetentionDecision decision)
        {
            return decision.Decision == "refused-ownership";
        }

        private static void AssertRetentionRoundTrip(InputRetentionDecision original)
        {
            var restored = JsonSerializer.Deserialize<InputRetentionDecision>(JsonSerializer.Serialize(original));
            Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
            Assert.Equal(original.Kind, restored.Kind);
            Assert.Equal(original.Identity, restored.Identity);
            Assert.Equal(original.Path, restored.Path);
            Assert.Equal(original.Decision, restored.Decision);
            Assert.Equal(original.Reason, restored.Reason);
            Assert.Equal(original.LeaseIds, restored.LeaseIds);
            Assert.Equal(original.Bytes, restored.Bytes);
            Assert.Equal(original.At, restored.At);
            Assert.Equal(original.WorkUnits, restored.WorkUnits);
            Assert.Equal(original.EntriesVisited, restored.EntriesVisited);
            Assert.Equal(original.AttributeReads, restored.AttributeReads);
            Assert.Equal(original.AttributeWrites, restored.AttributeWrites);
            Assert.Equal(original.FilesRemoved, restored.FilesRemoved);
            Assert.Equal(original.DirectoriesRemoved, restored.DirectoriesRemoved);
            Assert.Equal(original.BytesRemoved, restored.BytesRemoved);
            Assert.Equal(original.ElapsedSeconds, restored.ElapsedSeconds);
            Assert.Equal(original.Partial, restored.Partial);
            Assert.Equal(original.ControlRecordsRead, restored.ControlRecordsRead);
            Assert.Equal(original.ControlRecordsRemoved, restored.ControlRecordsRemoved);
        }

        /// <summary>A sealed physical generation survives disposal and an unchanged admission copies exactly zero input bytes.</summary>
        [Fact]
        public void ColdAndWarmAdmissionPreserveBytesIdentityAndMeasuredZeroCopies()
        {
            string root;
            string identity;
            string revision;
            string leaseId;
            using (var cold = fixture.Acquire())
            {
                root = cold.Root;
                identity = cold.PreparedInputId;
                revision = cold.SourceFingerprint;
                leaseId = cold.LeaseId;
                Assert.False(cold.Metrics.CacheHit);
                Assert.True(cold.Metrics.FilesCopied > 0);
                Assert.True(cold.Metrics.BytesCopied > 0);
                Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(root, fixture.Payload)));
                Assert.Empty(cold.VerifyUnchanged());
                Assert.NotEqual(fixture.Source, root);
            }

            Assert.True(Directory.Exists(root));
            using var warm = fixture.Acquire();
            Assert.Equal(root, warm.Root);
            Assert.Equal(identity, warm.PreparedInputId);
            Assert.Equal(revision, warm.SourceFingerprint);
            Assert.NotEqual(leaseId, warm.LeaseId);
            Assert.True(warm.Metrics.CacheHit);
            Assert.Equal(0, warm.Metrics.FilesCopied);
            Assert.Equal(0, warm.Metrics.BytesCopied);
            Assert.Equal(0, warm.Metrics.FullProjectCopies);
            Assert.True(warm.Metrics.ReusedInputs > 0);
            Assert.Empty(warm.VerifyUnchanged());
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(fixture.Source, fixture.Payload)));
        }

        /// <summary>A second reader must finish admission while the first still owns the same immutable physical generation.</summary>
        [Fact]
        public async Task ReadersOverlapWithoutCopyingOrReusingLeaseIdentity()
        {
            using var first = fixture.Acquire();
            preparation = Task.Run(fixture.Acquire);
            using var second = await preparation.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(first.Root, second.Root);
            Assert.Equal(first.PreparedInputId, second.PreparedInputId);
            Assert.NotEqual(first.LeaseId, second.LeaseId);
            Assert.Equal(0, second.Metrics.FilesCopied);
            Assert.Equal(0, second.Metrics.BytesCopied);
            Assert.Equal(0, second.Metrics.FullProjectCopies);
            Assert.Empty(first.VerifyUnchanged());
            Assert.Empty(second.VerifyUnchanged());
        }

        /// <summary>Same-length, same-timestamp edits cannot hide behind a live revision or be admitted as a warm cache hit.</summary>
        [Fact]
        public void ActiveRevisionRefusesSourceChangeAndUpdatesAfterItsReadersRelease()
        {
            string revision;
            using (var first = fixture.Acquire())
            {
                revision = first.SourceFingerprint;
                fixture.ChangeSource();
                var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
                Assert.Equal("prepared-revision-busy", refusal.Reason);
                Assert.Contains(refusal.Reason, refusal.Metrics.ConflictReasons);
                Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(first.Root, fixture.Payload)));
                Assert.Empty(first.VerifyUnchanged());
            }

            using var changed = fixture.Acquire();
            Assert.NotEqual(revision, changed.SourceFingerprint);
            Assert.False(changed.Metrics.CacheHit);
            Assert.Equal("{\"value\":2}", File.ReadAllText(Path.Combine(changed.Root, fixture.Payload)));
            Assert.Empty(changed.VerifyUnchanged());
        }

        /// <summary>Edits at an observed publication boundary refuse the mixed revision, then a clean retry can recover.</summary>
        [Theory]
        [InlineData("materialized")]
        [InlineData("before-seal")]
        public async Task SourceMutationDuringPreparationCannotPublishMixedInputs(string phase)
        {
            Start(phase);
            await gate.Ready;
            fixture.ChangeSource();
            gate.Release();
            var refusal = await Assert.ThrowsAsync<PreparedInputRefusal>(CompletePreparation);
            Assert.Equal("source-revision-changed", refusal.Reason);
            fixture.Observer = null;
            using var recovered = fixture.Acquire();
            Assert.Equal("{\"value\":2}", File.ReadAllText(Path.Combine(recovered.Root, fixture.Payload)));
            Assert.Empty(recovered.VerifyUnchanged());
        }

        /// <summary>Cancellation after materialization releases the preparation owner without sealing partial inputs.</summary>
        [Fact]
        public async Task CancelledPreparationReleasesItsLeaseAndAllowsRecovery()
        {
            Start("materialized");
            await gate.Ready;
            cancellation.Cancel();
            gate.Release();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(CompletePreparation);
            fixture.Observer = null;
            fixture.Token = default;
            using var recovered = fixture.Acquire();
            Assert.Empty(recovered.VerifyUnchanged());
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(recovered.Root, fixture.Payload)));
        }

        /// <summary>A deterministic I/O failure must remain a refusal and release ownership before the next admission.</summary>
        [Fact]
        public async Task ObserverFailureIsReportedAndDoesNotPoisonTheNextAdmission()
        {
            Start("materialized");
            await gate.Ready;
            gate.Fail = true;
            gate.Release();
            var refusal = await Assert.ThrowsAsync<PreparedInputRefusal>(CompletePreparation);
            Assert.Equal("preparation-failed", refusal.Reason);
            Assert.Contains(refusal.Reason, refusal.Metrics.ConflictReasons);
            fixture.Observer = null;
            using var recovered = fixture.Acquire();
            Assert.Empty(recovered.VerifyUnchanged());
        }

        /// <summary>Retention cannot remove a prepared root held by an actual live OS lease, even under zero limits.</summary>
        [Fact]
        public void RetentionProtectsActiveInputsThenRemovesOnlyTheOwnedReleasedGeneration()
        {
            string root;
            using (var active = fixture.Acquire())
            {
                root = active.Root;
                var decisions = PreparedInputStore.Prune(fixture.Cache, 0, 0, 0, DateTimeOffset.UtcNow.AddDays(1));
                Assert.Contains("retained-active", Decisions(decisions));
                Assert.True(Directory.Exists(root));
                Assert.Empty(active.VerifyUnchanged());
            }

            var released = PreparedInputStore.Prune(fixture.Cache, 0, 0, 0, DateTimeOffset.UtcNow.AddDays(1));
            Assert.Contains("removed", Decisions(released));
            Assert.False(Directory.Exists(root));
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(fixture.Source, fixture.Payload)));
        }

        /// <summary>Bypassing file protection in this owned fixture cannot make altered prepared bytes a valid warm input.</summary>
        [Fact]
        public void MutatedPreparedInputIsDetectedAndRefusedInsteadOfReused()
        {
            using (var lease = fixture.Acquire())
            {
                var path = Path.Combine(lease.Root, fixture.Payload);
                var timestamp = File.GetLastWriteTimeUtc(path);
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                File.WriteAllText(path, "{\"value\":9}");
                File.SetLastWriteTimeUtc(path, timestamp);
                Assert.Equal(new[] { fixture.Payload.Replace(Path.DirectorySeparatorChar, '/') }, lease.VerifyUnchanged());
            }

            var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
            Assert.Equal("prepared-input-mutated", refusal.Reason);
            Assert.Contains(refusal.Reason, refusal.Metrics.ConflictReasons);
            Assert.False(refusal.Metrics.CacheHit);
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(fixture.Source, fixture.Payload)));
            using var repaired = PreparedInputStore.Repair(fixture.Source, fixture.Cache);
            Assert.False(repaired.Metrics.CacheHit);
            Assert.Equal(new[] { "quarantine-repair" }, repaired.Metrics.ConflictReasons);
            Assert.True(repaired.Metrics.FilesCopied > 0);
            Assert.True(repaired.Metrics.BytesCopied > 0);
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(repaired.Root, fixture.Payload)));
            Assert.Empty(repaired.VerifyUnchanged());
            using var warm = fixture.Acquire();
            Assert.True(warm.Metrics.CacheHit);
            Assert.Equal(0, warm.Metrics.FilesCopied);
            Assert.Equal(0, warm.Metrics.BytesCopied);
            Assert.Equal(0, warm.Metrics.FullProjectCopies);
            Assert.Empty(warm.VerifyUnchanged());
        }

        /// <summary>An unknown empty leaf is an exact mutation; explicit repair removes it without copying intact files.</summary>
        [Fact]
        public void EmptyDirectoryMutationRequiresExplicitRepairAndPreservesDeclaredEmptyDirectory()
        {
            var declared = Path.Combine("mods", "probe", "declared_empty");
            var unexpected = Path.Combine("mods", "unexpected_empty");
            Directory.CreateDirectory(Path.Combine(fixture.Source, declared));
            using (var lease = fixture.Acquire())
            {
                Assert.True(Directory.Exists(Path.Combine(lease.Root, declared)));
                Assert.Empty(lease.VerifyUnchanged());
                Directory.CreateDirectory(Path.Combine(lease.Root, unexpected));
                Assert.Equal(new[] { "mods/unexpected_empty" }, lease.VerifyUnchanged());
            }

            var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
            Assert.Equal("prepared-input-mutated", refusal.Reason);
            using var repaired = PreparedInputStore.Repair(fixture.Source, fixture.Cache);
            Assert.False(Directory.Exists(Path.Combine(repaired.Root, unexpected)));
            Assert.True(Directory.Exists(Path.Combine(repaired.Root, declared)));
            Assert.Equal(0, repaired.Metrics.FilesCopied);
            Assert.Equal(0, repaired.Metrics.BytesCopied);
            Assert.Equal(0, repaired.Metrics.FullProjectCopies);
            Assert.Equal(new[] { "quarantine-repair" }, repaired.Metrics.ConflictReasons);
            Assert.Empty(repaired.VerifyUnchanged());
            Assert.False(Directory.Exists(Path.Combine(fixture.Source, unexpected)));
            Assert.Equal("{\"value\":1}", File.ReadAllText(Path.Combine(repaired.Root, fixture.Payload)));
        }

        /// <summary>A directory-only source revision cannot be admitted behind a live reader or charged as copied file bytes.</summary>
        [Fact]
        public void EmptySourceDirectoryChangesRevisionAndWaitsForExistingReader()
        {
            string revision;
            using (var lease = fixture.Acquire())
            {
                revision = lease.SourceFingerprint;
                Directory.CreateDirectory(Path.Combine(fixture.Source, "mods", "probe", "source_empty"));
                var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
                Assert.Equal("prepared-revision-busy", refusal.Reason);
                Assert.False(Directory.Exists(Path.Combine(lease.Root, "mods", "probe", "source_empty")));
                Assert.Empty(lease.VerifyUnchanged());
            }

            using var updated = fixture.Acquire();
            Assert.NotEqual(revision, updated.SourceFingerprint);
            Assert.True(Directory.Exists(Path.Combine(updated.Root, "mods", "probe", "source_empty")));
            Assert.Equal(0, updated.Metrics.FilesCopied);
            Assert.Equal(0, updated.Metrics.BytesCopied);
            Assert.Equal(0, updated.Metrics.FullProjectCopies);
            Assert.Empty(updated.VerifyUnchanged());
        }

        /// <summary>Actual SDK output parents and named immutable obj scratch survive explicit repair without invented receipts.</summary>
        [Fact]
        public void ActualBuildParentsAndImmutableObjScratchSurviveDirectoryRepair()
        {
            File.WriteAllText(Path.Combine(fixture.Source, "Cthangover.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
                + "</PropertyGroup></Project>");
            Directory.CreateDirectory(Path.Combine(fixture.Source, "Core"));
            File.WriteAllText(Path.Combine(fixture.Source, "Core", "Program.cs"),
                "namespace ExecutionFixture\n{\n    /// <summary>Compiled input for public preparation receipt verification.</summary>\n"
                + "    public static class BuildValue\n    {\n        /// <summary>Stable fixture revision.</summary>\n"
                + "        public const int Current = 1;\n    }\n}\n");
            fixture.Build = new PreparedBuildRequest { ArtifactsDirectory = Path.Combine(fixture.Root, "actual-build") };
            string binaryDigest;
            using (var lease = fixture.Acquire())
            {
                var assembly = Path.Combine(lease.Root, "bin", "Debug", "net10.0", "Cthangover.dll");
                Assert.True(File.Exists(Path.Combine(lease.Root, "Core", "Program.cs")), "SDK source admitted by the real input policy");
                Assert.True(File.Exists(assembly));
                binaryDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)));
                Assert.Empty(lease.VerifyUnchanged());
                Directory.CreateDirectory(Path.Combine(lease.Root, "obj", "scratch_empty"));
                Assert.Empty(lease.VerifyUnchanged());
                Directory.CreateDirectory(Path.Combine(lease.Root, "mods", "unexpected_empty"));
                Assert.Equal(new[] { "mods/unexpected_empty" }, lease.VerifyUnchanged());
            }

            fixture.Build.NoBuild = true;
            fixture.Build.ArtifactsDirectory = Path.Combine(fixture.Root, "repair-build");
            using var repaired = PreparedInputStore.Repair(fixture.Source, fixture.Cache, fixture.Build);
            Assert.False(Directory.Exists(Path.Combine(repaired.Root, "mods", "unexpected_empty")));
            Assert.True(Directory.Exists(Path.Combine(repaired.Root, "obj", "scratch_empty")));
            var restored = Path.Combine(repaired.Root, "bin", "Debug", "net10.0", "Cthangover.dll");
            Assert.True(File.Exists(restored));
            Assert.Equal(binaryDigest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(restored))));
            Assert.True(repaired.Metrics.BuildCacheHit);
            Assert.Equal(0, repaired.Metrics.FilesCopied);
            Assert.Equal(0, repaired.Metrics.BytesCopied);
            Assert.Equal(0, repaired.Metrics.FullProjectCopies);
            Assert.Empty(repaired.VerifyUnchanged());
        }

        /// <summary>Executes rebuilt values after same-metadata edits; changed receipts cannot authorize stale binaries.</summary>
        [Fact]
        public void SameMetadataSourceEditRebuildsActualExecutableAndWarmNoBuildRunsNewValue()
        {
            File.WriteAllText(Path.Combine(fixture.Source, "Cthangover.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
                + "<OutputType>Exe</OutputType></PropertyGroup></Project>");
            Directory.CreateDirectory(Path.Combine(fixture.Source, "Core"));
            var program = Path.Combine(fixture.Source, "Core", "Program.cs");
            var original = "namespace ExecutionFixture\n{\n    using System;\n"
                + "    /// <summary>Executable revision exposes stale compiler output despite a changed receipt.</summary>\n"
                + "    internal static class Program\n    {\n        private static void Main()\n        {\n"
                + "            Console.WriteLine(\"revision-one\");\n        }\n    }\n}\n";
            File.WriteAllText(program, original);
            var timestamp = File.GetLastWriteTimeUtc(program);
            var length = new FileInfo(program).Length;
            fixture.Build = new PreparedBuildRequest { ArtifactsDirectory = Path.Combine(fixture.Root, "build-v1") };
            string firstRevision;
            string firstBuild;
            using (var first = fixture.Acquire())
            {
                Assert.Equal(original, File.ReadAllText(Path.Combine(first.Root, "Core", "Program.cs")));
                Assert.Equal("revision-one", ExecutePrepared(first));
                firstRevision = first.SourceFingerprint;
                firstBuild = first.Metrics.BuildIdentity;
                Assert.Empty(first.VerifyUnchanged());
            }

            var changed = original.Replace("revision-one", "revision-two", StringComparison.Ordinal);
            File.WriteAllText(program, changed);
            File.SetLastWriteTimeUtc(program, timestamp);
            Assert.Equal(length, new FileInfo(program).Length);
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(program));
            fixture.Build.NoBuild = true;
            fixture.Build.ArtifactsDirectory = Path.Combine(fixture.Root, "stale-no-build");
            var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
            Assert.Equal("build-input-mismatch", refusal.Reason);
            Assert.Contains(refusal.Reason, refusal.Metrics.ConflictReasons);
            fixture.Build.NoBuild = false;
            fixture.Build.ArtifactsDirectory = Path.Combine(fixture.Root, "build-v2");
            using var rebuilt = fixture.Acquire();
            Assert.NotEqual(firstRevision, rebuilt.SourceFingerprint);
            Assert.NotEqual(firstBuild, rebuilt.Metrics.BuildIdentity);
            Assert.False(rebuilt.Metrics.BuildCacheHit);
            Assert.Equal(changed, File.ReadAllText(Path.Combine(rebuilt.Root, "Core", "Program.cs")));
            Assert.Equal("revision-two", ExecutePrepared(rebuilt));
            Assert.Empty(rebuilt.VerifyUnchanged());
            fixture.Build.NoBuild = true;
            fixture.Build.ArtifactsDirectory = Path.Combine(fixture.Root, "warm-v2");
            using var warm = fixture.Acquire();
            Assert.Equal(rebuilt.SourceFingerprint, warm.SourceFingerprint);
            Assert.Equal(rebuilt.Metrics.BuildIdentity, warm.Metrics.BuildIdentity);
            Assert.True(warm.Metrics.CacheHit);
            Assert.True(warm.Metrics.BuildCacheHit);
            Assert.Equal(0, warm.Metrics.FilesCopied);
            Assert.Equal(0, warm.Metrics.BytesCopied);
            Assert.Equal(0, warm.Metrics.FullProjectCopies);
            Assert.Equal("revision-two", ExecutePrepared(warm));
            Assert.Empty(warm.VerifyUnchanged());
        }

        /// <summary>
        /// Real nested IDE outputs join the hash receipt; warm no-build copies nothing and neither tampering nor unlisted files hide.
        /// </summary>
        [Fact]
        public void ActualGeneratedIdeBuildOutputsAreReceiptedFencedAndReusedWithoutCopies()
        {
            File.WriteAllText(Path.Combine(fixture.Source, "Cthangover.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>"
                + "<BaseOutputPath>.ide/bin/source_fixture/Nested/</BaseOutputPath></PropertyGroup>"
                + "<Target Name=\"WriteFixtureReference\" AfterTargets=\"Build\">"
                + "<MakeDir Directories=\"$(TargetDir)mod_reference_assemblies/net10.0\" />"
                + "<WriteLinesToFile File=\"$(TargetDir)mod_reference_assemblies/net10.0/marker.txt\" "
                + "Lines=\"generated-reference\" Overwrite=\"true\" /></Target></Project>");
            Directory.CreateDirectory(Path.Combine(fixture.Source, "Core"));
            File.WriteAllText(Path.Combine(fixture.Source, "Core", "Program.cs"),
                "namespace ExecutionFixture\n{\n    public static class Value\n    {\n"
                + "        public const int Current = 1;\n    }\n}\n");
            fixture.Build = new PreparedBuildRequest { ArtifactsDirectory = Path.Combine(fixture.Root, "ide-build") };
            const string assembly = ".ide/bin/source_fixture/Nested/Debug/net10.0/Cthangover.dll";
            const string reference = ".ide/bin/source_fixture/Nested/Debug/net10.0/mod_reference_assemblies/net10.0/marker.txt";
            string compiled;
            byte[] original;
            using (var first = fixture.Acquire())
            {
                compiled = Path.Combine(first.Root, assembly);
                Assert.True(File.Exists(compiled));
                Assert.Equal("generated-reference", File.ReadAllText(Path.Combine(first.Root, reference)).Trim());
                var receipt = JsonSerializer.Deserialize<PreparedInputManifest>(File.ReadAllText(first.Metrics.ManifestPath),
                    ReportWriter.JsonOptions);
                Assert.NotNull(receipt.Build);
                var recorded = new Dictionary<string, PreparedInputFile>(StringComparer.Ordinal);
                foreach (var output in receipt.Build.Outputs)
                {
                    recorded.Add(output.Path, output);
                    Assert.Equal(output.Hash, Convert.ToHexString(SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(first.Root, output.Path)))).ToLowerInvariant());
                }

                Assert.Contains(assembly, recorded.Keys);
                Assert.Contains(reference, recorded.Keys);
                Assert.Empty(first.VerifyUnchanged());
                original = File.ReadAllBytes(compiled);
            }

            fixture.Build.NoBuild = true;
            using (var warm = fixture.Acquire())
            {
                Assert.True(warm.Metrics.CacheHit);
                Assert.True(warm.Metrics.BuildCacheHit);
                Assert.Equal(0, warm.Metrics.FilesCopied);
                Assert.Equal(0, warm.Metrics.BytesCopied);
                Assert.Equal(0, warm.Metrics.FullProjectCopies);
                Assert.Empty(warm.VerifyUnchanged());
            }

            var timestamp = File.GetLastWriteTimeUtc(compiled);
            var altered = (byte[])original.Clone();
            altered[^1] ^= 1;
            File.SetAttributes(compiled, File.GetAttributes(compiled) & ~FileAttributes.ReadOnly);
            File.WriteAllBytes(compiled, altered);
            File.SetLastWriteTimeUtc(compiled, timestamp);
            var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
            Assert.Equal("build-artifact-mismatch", refusal.Reason);
            Assert.False(refusal.Metrics.BuildCacheHit);
            File.WriteAllBytes(compiled, original);
            File.SetLastWriteTimeUtc(compiled, timestamp);
            const string unlisted = ".ide/bin/source_fixture/Nested/Debug/net10.0/unlisted.txt";
            using (var verified = fixture.Acquire())
            {
                Assert.Empty(verified.VerifyUnchanged());
                File.WriteAllText(Path.Combine(verified.Root, unlisted), "unexpected");
                Assert.Equal(new[] { unlisted }, verified.VerifyUnchanged());
            }

            Assert.Equal("prepared-input-mutated", Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease).Reason);
            using var repaired = PreparedInputStore.Repair(fixture.Source, fixture.Cache, fixture.Build);
            Assert.Equal(0, repaired.Metrics.FilesCopied);
            Assert.Equal(0, repaired.Metrics.BytesCopied);
            Assert.Equal(0, repaired.Metrics.FullProjectCopies);
            Assert.Equal(1, repaired.Metrics.FilesRemoved);
            Assert.True(repaired.Metrics.BuildCacheHit);
            Assert.False(File.Exists(Path.Combine(repaired.Root, unlisted)));
            Assert.Empty(repaired.VerifyUnchanged());
        }

        /// <summary>No-build cannot turn an unbuilt fixture into an admitted executable merely because input files exist.</summary>
        [Fact]
        public void NoBuildRequiresAValidatedBuildReceipt()
        {
            fixture.Build = new PreparedBuildRequest
            {
                NoBuild = true,
                ArtifactsDirectory = Path.Combine(fixture.Root, "build-artifacts")
            };
            var refusal = Assert.Throws<PreparedInputRefusal>(fixture.AcquireAndRelease);
            Assert.Equal("build-receipt-missing", refusal.Reason);
            Assert.Contains(refusal.Reason, refusal.Metrics.ConflictReasons);
            fixture.Build = null;
            using var recovered = fixture.Acquire();
            Assert.Empty(recovered.VerifyUnchanged());
        }

        /// <summary>Preparation metrics survive the same public JSON options used by final runner reports, field for field.</summary>
        [Fact]
        public void ActualPreparationMetricsRoundTripWithoutLosingCountersOrOwnership()
        {
            using var lease = fixture.Acquire();
            var original = lease.Metrics;
            var json = JsonSerializer.Serialize(original, ReportWriter.JsonOptions);
            var restored = JsonSerializer.Deserialize<InputPreparationMetrics>(json, ReportWriter.JsonOptions);
            Assert.NotNull(restored);
            Assert.Equal(json, JsonSerializer.Serialize(restored, ReportWriter.JsonOptions));
            Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
            Assert.Equal(lease.Root, restored.PreparedRoot);
            Assert.Equal(lease.LeaseId, restored.LeaseId);
            Assert.Equal(lease.PreparedInputId, restored.PreparedInputId);
            Assert.Equal(lease.SourceFingerprint, restored.SourceFingerprint);
            Assert.Equal(original.FilesCopied, restored.FilesCopied);
            Assert.Equal(original.BytesCopied, restored.BytesCopied);
        }

        private static string ExecutePrepared(PreparedInputLease lease)
        {
            var assembly = Path.Combine(lease.Root, "bin", "Debug", "net10.0", "Cthangover.dll");
            Assert.True(File.Exists(assembly));
            var executed = ProcessRunner.Run("dotnet", new[] { assembly }, lease.Root, 30);
            Assert.Equal(0, executed.ExitCode);
            Assert.False(executed.TimedOut);
            Assert.Single(executed.Output);
            return executed.OutputText.Trim();
        }

        private void Start(string phase)
        {
            fixture.Token = cancellation.Token;
            gate = new PreparationGate(phase, cancellation.Token);
            fixture.Observer = gate;
            preparation = Task.Run(fixture.Acquire);
        }

        private async Task CompletePreparation()
        {
            using var lease = await preparation;
        }

        private static string[] Decisions(System.Collections.Generic.IReadOnlyList<InputRetentionDecision> decisions)
        {
            var values = new string[decisions.Count];
            for (var index = 0; index < decisions.Count; index++)
            {
                values[index] = decisions[index].Decision;
            }

            return values;
        }

        /// <summary>Unblocks and joins any pending preparation before removing its fixture, including failed assertions.</summary>
        public void Dispose()
        {
            cancellation.Cancel();
            gate?.Release();
            if (preparation != null)
            {
                try
                {
                    preparation.GetAwaiter().GetResult().Dispose();
                }
                catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException)
                {
                }
            }

            gate?.Dispose();
            cancellation.Dispose();
            fixture.Dispose();
        }
    }
}
