namespace Cthangover.TestRunner
{
    using System.Diagnostics;

    /// <summary>Shared read ownership pins a complete generation against preparation, build and retention writers.</summary>
    public sealed class PreparedInputLease : IDisposable
    {
        private FileStream gate;
        private readonly PreparedInputManifest manifest;
        private readonly string record;
        private readonly PreparedInputOwner owner;

        internal PreparedInputLease(string root, string store, FileStream gate, PreparedInputManifest manifest,
            InputPreparationMetrics metrics)
        {
            Root = root;
            this.gate = gate;
            this.manifest = manifest;
            Metrics = metrics;
            LeaseId = Guid.NewGuid().ToString("N");
            metrics.LeaseId = LeaseId;
            using var process = Process.GetCurrentProcess();
            owner = new PreparedInputOwner
            {
                LeaseId = LeaseId,
                RunId = LeaseId,
                PreparedInputId = PreparedInputId,
                Root = root,
                Access = "read",
                ProcessId = process.Id,
                ProcessStartedTicks = process.StartTime.ToUniversalTime().Ticks,
                Active = true,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            record = Path.Combine(store, "leases", LeaseId + ".json");
            PreparedInputJson.Write(record, owner);
            PreparedInputJson.Write(Path.Combine(store, "last-used.json"), DateTimeOffset.UtcNow);
        }

        /// <summary>Borrowed prepared project root; input files must never be modified by read/output children.</summary>
        public string Root
        {
            get;
        }

        /// <summary>Verified source content and preparation-policy digest.</summary>
        public string SourceFingerprint => manifest.SourceFingerprint;

        /// <summary>Versioned identity of this sealed input generation.</summary>
        public string PreparedInputId => "inputs-v1-" + SourceFingerprint;

        /// <summary>Unique active owner token, also present in durable lease evidence.</summary>
        public string LeaseId
        {
            get;
        }

        /// <summary>Actual work performed by this acquisition, including cold/update copies and checked build reuse.</summary>
        public InputPreparationMetrics Metrics
        {
            get;
        }

        internal PreparedInputManifest Manifest => manifest;

        internal void SetRunId(string runId)
        {
            owner.RunId = runId;
            PreparedInputJson.Write(record, owner);
        }

        /// <summary>Returns exact relative mutations; a changed generation is quarantined instead of silently shared again.</summary>
        public IReadOnlyList<string> VerifyUnchanged()
        {
            ObjectDisposedException.ThrowIf(gate == null, this);
            var changes = PreparedInputStore.Verify(Root, manifest);
            if (changes.Count > 0)
            {
                PreparedInputJson.Write(Path.Combine(Path.GetDirectoryName(Root), "quarantined.json"), changes);
            }

            return changes;
        }

        /// <summary>Releases only ownership, preserving the prepared tree for later unchanged invocations.</summary>
        public void Dispose()
        {
            if (gate == null)
            {
                return;
            }

            try
            {
                owner.Active = false;
                owner.UpdatedAt = DateTimeOffset.UtcNow;
                PreparedInputJson.Write(record, owner);
            }
            finally
            {
                gate.Dispose();
                gate = null;
            }
        }
    }
}
