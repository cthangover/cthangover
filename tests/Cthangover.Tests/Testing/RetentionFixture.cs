namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using Cthangover.TestRunner;

    /// <summary>Owns tiny real prepared stores and cancels retention only at an observed completed deletion boundary.</summary>
    internal sealed class RetentionFixture : IDisposable, IInputRetentionObserver
    {
        private readonly PreparedInputFixture inputs = new();
        private readonly CancellationTokenSource cancellation = new();

        internal string Cache => inputs.Cache;

        internal string Source => inputs.Source;

        internal string PreparedRoot { get; private set; }

        internal RetentionWorkOptions Work { get; } = new();

        internal bool CancelAfterDeletion { get; set; }

        internal int MaxCount { get; set; }

        internal long MaxBytes { get; set; }

        internal double MaxDays { get; set; }

        internal List<InputRetentionDecision> Observed { get; } = new();

        internal RetentionFixture(int files = 128)
        {
            var branch = Path.Combine(Source, "Core", "retention", "nested", "leaf");
            Directory.CreateDirectory(branch);
            for (var index = 0; index < files; index++)
            {
                File.WriteAllText(Path.Combine(branch, "file_" + index + ".cs"), "// tiny payload\n");
            }
        }

        internal PreparedInputLease Acquire()
        {
            var lease = inputs.Acquire();
            PreparedRoot = lease.Root;
            return lease;
        }

        internal void Prepare()
        {
            using var lease = Acquire();
        }

        internal void PreparePeer()
        {
            var source = Path.Combine(inputs.Root, "peer");
            Directory.CreateDirectory(Path.Combine(source, "Core"));
            File.WriteAllText(Path.Combine(source, "project.godot"), "config_version=5\n");
            File.WriteAllText(Path.Combine(source, "Core", "Peer.cs"), "// independent tiny store\n");
            using var peer = PreparedInputStore.Acquire(source, Cache);
        }

        internal void AcquireAndRelease()
        {
            using var lease = Acquire();
        }

        internal void RepairAndRelease()
        {
            using var lease = PreparedInputStore.Repair(Source, Cache);
        }

        internal IReadOnlyList<InputRetentionDecision> Prune()
        {
            return PreparedInputStore.Prune(Cache, MaxCount, MaxDays, MaxBytes, work: Work,
                cancellationToken: cancellation.Token, observer: this);
        }

        internal void PruneAndDiscard()
        {
            Prune();
        }

        internal void Cancel()
        {
            cancellation.Cancel();
        }

        internal SortedDictionary<string, byte[]> StoreInventory()
        {
            var store = Path.GetDirectoryName(PreparedRoot);
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(store, "*", SearchOption.AllDirectories))
            {
                files[Path.GetRelativePath(store, path).Replace('\\', '/')] = File.ReadAllBytes(path);
            }

            return files;
        }

        internal void RecordEvidence(Dictionary<string, object> record)
        {
            var directory = Environment.GetEnvironmentVariable("CTHANGOVER_RETENTION_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(record);
                var restored = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                using var original = JsonDocument.Parse(json);
                Assert.True(JsonElement.DeepEquals(original.RootElement, JsonSerializer.SerializeToElement(restored)));
                File.WriteAllText(Path.Combine(directory, "measurement.json"), json);
            }
        }

        /// <summary>Copies evidence at the synchronous boundary; cancellation never depends on elapsed sleeps or polling.</summary>
        public void Observe(string phase, string store, InputRetentionDecision decision)
        {
            Observed.Add(JsonSerializer.Deserialize<InputRetentionDecision>(JsonSerializer.Serialize(decision)));
            if (CancelAfterDeletion && phase == "file-deleted")
            {
                cancellation.Cancel();
            }
        }

        /// <summary>Only this fixture's GUID source/cache is removed, after test leases and deterministic callbacks finish.</summary>
        public void Dispose()
        {
            cancellation.Dispose();
            inputs.Dispose();
        }
    }
}
