namespace Cthangover.TestRunner
{
    /// <summary>Parent's atomic pre-start and observed terminal evidence; core startup/exit alone never determines PASS.</summary>
    public sealed class LaunchExecutionReceipt
    {
        /// <summary>Shared startup receipt version consumed by Core before logger/settings initialization.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Unique invocation identity supplied before process creation.</summary>
        public string RunId
        {
            get; set;
        }

        /// <summary>Original catalogue selector.</summary>
        public string TestId
        {
            get; set;
        }

        /// <summary>Attempt identity, never a reused output root.</summary>
        public string AttemptId
        {
            get; set;
        }

        /// <summary>Preallocated unique process token.</summary>
        public string LaunchId
        {
            get; set;
        }

        /// <summary>Native engine sink; never the managed game's main log.</summary>
        public string NativeLogPath
        {
            get; set;
        }

        /// <summary>Actual engine-side arguments before the user separator.</summary>
        public string[] EngineArguments { get; set; } = Array.Empty<string>();

        /// <summary>Parent phase: starting/exited/timed_out/cancelled/failed_to_start.</summary>
        public string Status { get; set; } = "starting";

        /// <summary>Actual child PID, absent until the OS creates the process.</summary>
        public int? ProcessId
        {
            get; set;
        }

        /// <summary>Observed child exit, not inferred from core's advisory process-exit file.</summary>
        public int? ExitCode
        {
            get; set;
        }

        /// <summary>Actual OS exit when observed; never the compatibility infrastructure sentinel.</summary>
        public int? ObservedExitCode { get; set; }

        /// <summary>Whether parent supervision actually proved the root process exited.</summary>
        public bool ExitConfirmed { get; set; }

        /// <summary>Native creation time used together with PID for durable containment identity.</summary>
        public DateTimeOffset? ProcessCreatedAt { get; set; }

        /// <summary>Whether stdout reached EOF, independently of root exit.</summary>
        public bool StdoutCompleted { get; set; }

        /// <summary>Whether stderr reached EOF, independently of root exit.</summary>
        public bool StderrCompleted { get; set; }

        /// <summary>False forbids successful fences and cache/lane reuse even if the root's OS exit was zero.</summary>
        public bool CleanupSafe { get; set; }

        /// <summary>not-started, unproven-exit, unfinished-pipes or confirmed-exit supervision state.</summary>
        public string TerminationState { get; set; } = "not-started";

        /// <summary>Measured launch wall time.</summary>
        public double ElapsedSeconds
        {
            get; set;
        }

        /// <summary>Sampled child root-process CPU seconds, absent when unavailable.</summary>
        public double? CpuSeconds
        {
            get; set;
        }

        /// <summary>Sampled root-process peak resident memory in MiB.</summary>
        public double? PeakMemoryMb
        {
            get; set;
        }

        /// <summary>Input mutations observed by the parent fence; readiness does not erase a mutation.</summary>
        public List<string> FenceChanges { get; set; } = new();

        /// <summary>Immutable baseline mutations, checked separately from legitimate persistent-write changes.</summary>
        public List<string> ImmutableFenceChanges { get; set; } = new();

        /// <summary>not-checked, immutable-unchanged, immutable-violation or write-lane-delta.</summary>
        public string FenceClassification { get; set; } = "not-checked";

        /// <summary>Per-launch paths and preparation identities, independent of the terminal result contract.</summary>
        public LaunchExecutionMetrics Execution
        {
            get; set;
        }
    }
}
