namespace Cthangover.TestRunner
{
    /// <summary>Actual input transfers and checked identity; file/log/OS disk writes are deliberately separate measurements.</summary>
    public sealed class InputPreparationMetrics
    {
        /// <summary>Stable consumer contract for this additive metrics object.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Ordered input content and preparation-policy digest, independent of git state and timestamps.</summary>
        public string SourceFingerprint
        {
            get; set;
        }

        /// <summary>Identity of the sealed prepared generation acquired by this invocation.</summary>
        public string PreparedInputId
        {
            get; set;
        }

        /// <summary>True only when an already verified prepared generation was admitted without repair.</summary>
        public bool CacheHit
        {
            get; set;
        }

        /// <summary>Successfully materialized input files; includes repair and initial preparation, not metadata receipts.</summary>
        public long FilesCopied
        {
            get; set;
        }

        /// <summary>Actual bytes in those successful materializations; not estimated logical tree size or physical disk traffic.</summary>
        public long BytesCopied
        {
            get; set;
        }

        /// <summary>Initial complete input-tree populations; warm reuse and incremental repairs are zero.</summary>
        public int FullProjectCopies
        {
            get; set;
        }

        /// <summary>Actual obsolete input files removed during an exclusive revision upgrade.</summary>
        public long FilesRemoved
        {
            get; set;
        }

        /// <summary>Input files admitted from existing checked storage without a copy.</summary>
        public long ReusedInputs
        {
            get; set;
        }

        /// <summary>Preparation wall time, including integrity work and waiting/refusal boundaries.</summary>
        public double PrepareSeconds
        {
            get; set;
        }

        /// <summary>Runner CPU consumed during preparation, excluding compiler child CPU.</summary>
        public double CpuSeconds
        {
            get; set;
        }

        /// <summary>Largest sampled runner working set during preparation, in MiB.</summary>
        public double PeakMemoryMb
        {
            get; set;
        }

        /// <summary>Actual build-child wall time; zero when an existing verified receipt was reused.</summary>
        public double BuildSeconds
        {
            get; set;
        }

        /// <summary>Compiler child root-process CPU, distinct from preparation CPU and physical IO.</summary>
        public double? BuildCpuSeconds
        {
            get; set;
        }

        /// <summary>Compiler child sampled root-process peak in MiB.</summary>
        public double? BuildPeakMemoryMb
        {
            get; set;
        }

        internal void Sample()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            PeakMemoryMb = Math.Max(PeakMemoryMb, process.WorkingSet64 / 1048576.0);
        }

        /// <summary>Verified effective compiler/toolchain identity, or null for a pure preparation fixture.</summary>
        public string BuildIdentity
        {
            get; set;
        }

        /// <summary>True when matching output hashes and a build receipt avoided invoking a new build.</summary>
        public bool BuildCacheHit
        {
            get; set;
        }

        /// <summary>Live cross-process input lease token; JSON evidence does not replace the OS lease.</summary>
        public string LeaseId
        {
            get; set;
        }

        /// <summary>Explicit execution policy; never silently labels a fresh full copy as shared reuse.</summary>
        public string WorkspaceMode { get; set; } = "prepared-read";

        /// <summary>Physical sealed project reused by native read consumers.</summary>
        public string PreparedRoot
        {
            get; set;
        }

        /// <summary>Owned persistent store root, separate from live source and per-launch outputs.</summary>
        public string CacheRoot
        {
            get; set;
        }

        /// <summary>Published manifest binding the generation to checked input and build bytes.</summary>
        public string ManifestPath
        {
            get; set;
        }

        /// <summary>Explicit refusal/repair explanations; missing counters never imply zero copies.</summary>
        public List<string> ConflictReasons { get; set; } = new();
    }
}
