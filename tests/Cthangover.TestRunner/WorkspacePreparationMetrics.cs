namespace Cthangover.TestRunner
{
    /// <summary>Lane-level input transfers; restore counters are a subset of transfers, not additional copies.</summary>
    public sealed class WorkspacePreparationMetrics
    {
        /// <summary>Kernel/durable lane owner token.</summary>
        public string LeaseId
        {
            get; set;
        }

        /// <summary>Scheduler worker identity, independent of a test's dependency component.</summary>
        public string WorkerId
        {
            get; set;
        }

        /// <summary>Explicit prepared-read/output or persistent-write execution policy.</summary>
        public string WorkspaceMode
        {
            get; set;
        }

        /// <summary>Actual prepared or independent writable project root.</summary>
        public string Root
        {
            get; set;
        }

        /// <summary>Immutable baseline generation this lane was prepared from.</summary>
        public string PreparedInputId
        {
            get; set;
        }

        /// <summary>All successful lane input file copies, including initial population and restoration.</summary>
        public long FilesCopied
        {
            get; set;
        }

        /// <summary>Actual bytes in the lane copies above.</summary>
        public long BytesCopied
        {
            get; set;
        }

        /// <summary>Initial independent lane populations; later delta restores do not count as full copies.</summary>
        public int FullProjectCopies
        {
            get; set;
        }

        /// <summary>Actual added/obsolete lane files deleted by delta restoration.</summary>
        public long FilesRemoved
        {
            get; set;
        }

        /// <summary>Checked input files admitted without copying.</summary>
        public long ReusedInputs
        {
            get; set;
        }

        /// <summary>Lane preparation wall time, distinct from child execution and baseline preparation.</summary>
        public double PrepareSeconds
        {
            get; set;
        }

        /// <summary>Copies attributable to delta restoration, already included in FilesCopied.</summary>
        public long RestoreFiles
        {
            get; set;
        }

        /// <summary>Bytes attributable to delta restoration, already included in BytesCopied.</summary>
        public long RestoreBytes
        {
            get; set;
        }

        /// <summary>Measured component-boundary delta restoration wall time.</summary>
        public double RestoreSeconds
        {
            get; set;
        }

        /// <summary>Explicit refusals/conflicts; absence of a field is never interpreted as an empty list by consumers.</summary>
        public List<string> ConflictReasons { get; set; } = new();
    }
}
