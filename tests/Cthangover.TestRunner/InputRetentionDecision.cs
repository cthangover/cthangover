namespace Cthangover.TestRunner
{
    /// <summary>Auditable scoped retention decision; an unrecognized path or active lease never becomes a prune target.</summary>
    public sealed class InputRetentionDecision
    {
        /// <summary>Version of the independent retention record.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Prepared-input, write-lane or run-output ownership category.</summary>
        public string Kind
        {
            get; set;
        }

        /// <summary>Checked generation/lane/run identity.</summary>
        public string Identity
        {
            get; set;
        }

        /// <summary>Actual candidate root under the explicit owned cache.</summary>
        public string Path
        {
            get; set;
        }

        /// <summary>retained-active, retained-recent, removed, deferred or refused-ownership.</summary>
        public string Decision
        {
            get; set;
        }

        /// <summary>Concrete lease/limit/ownership reason for this decision.</summary>
        public string Reason
        {
            get; set;
        }

        /// <summary>Owner tokens observed while deciding; kernel leases remain authoritative.</summary>
        public string[] LeaseIds { get; set; } = Array.Empty<string>();

        /// <summary>Logical retained bytes measured for this candidate, distinct from copy/write counters.</summary>
        public long Bytes
        {
            get; set;
        }

        /// <summary>UTC decision time.</summary>
        public DateTimeOffset At
        {
            get; set;
        }

        /// <summary>Actual explicit work units charged to the call-wide budget, not estimated OS syscalls.</summary>
        public long WorkUnits { get; set; }

        /// <summary>Filesystem enumeration entries actually visited.</summary>
        public long EntriesVisited { get; set; }

        /// <summary>Explicit GetAttributes calls, separate from cached enumeration attributes and OS telemetry.</summary>
        public long AttributeReads { get; set; }

        /// <summary>Actual attribute updates needed to remove readonly files.</summary>
        public long AttributeWrites { get; set; }

        /// <summary>Files whose deletion completed in this call.</summary>
        public long FilesRemoved { get; set; }

        /// <summary>Directories whose deletion completed in this call.</summary>
        public long DirectoriesRemoved { get; set; }

        /// <summary>Logical lengths of successfully deleted files, never copied bytes or physical disk writes.</summary>
        public long BytesRemoved { get; set; }

        /// <summary>Observed candidate wall time, including synchronous OS calls that exceeded a cooperative boundary.</summary>
        public double ElapsedSeconds { get; set; }

        /// <summary>True when deletion began but the retiring store still needs another owned cleanup pass.</summary>
        public bool Partial { get; set; }

        /// <summary>Validated registry JSON records actually opened, separate from payload enumeration.</summary>
        public long ControlRecordsRead { get; set; }

        /// <summary>Validated inactive lease records removed under the exclusive gate; external run evidence is untouched.</summary>
        public long ControlRecordsRemoved { get; set; }
    }
}
