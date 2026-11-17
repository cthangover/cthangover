namespace Cthangover.TestRunner
{
    /// <summary>
    /// Captured outcome of an external process: merged stdout/stderr lines
    /// (Godot writes its logs to stdout, MSBuild errors to stderr), exit code,
    /// duration and whether the timeout had to kill it.
    /// </summary>
    public class ProcessResult
    {
        /// <summary>Actual observed child exit, or an infrastructure/timeout sentinel when no normal exit was obtained.</summary>
        public int ExitCode
        {
            get; set;
        }
        /// <summary>Actual root child PID; null when process creation failed.</summary>
        public int? ProcessId
        {
            get; set;
        }

        /// <summary>True only after Process.Start allocated the root PID; a failed start has no child to contain.</summary>
        public bool Started => ProcessId.HasValue;

        /// <summary>Actual OS root-process creation identity, when available; absence is never replaced with parent wall time.</summary>
        public DateTimeOffset? ProcessCreatedAt
        {
            get; set;
        }

        /// <summary>True only after the supervisor observed root termination and read its actual OS exit code.</summary>
        public bool ExitConfirmed
        {
            get; set;
        }

        /// <summary>Actual OS code when ExitConfirmed; unlike ExitCode, this never contains a timeout/infrastructure sentinel.</summary>
        public int? ObservedExitCode
        {
            get; set;
        }

        /// <summary>End-of-stream callback observed for stdout; root exit alone cannot prove descendants closed it.</summary>
        public bool StdoutCompleted
        {
            get; set;
        }

        /// <summary>End-of-stream callback observed for stderr independently of stdout.</summary>
        public bool StderrCompleted
        {
            get; set;
        }

        /// <summary>Both redirected streams reached their observed EOF boundary.</summary>
        public bool PipesCompleted => StdoutCompleted && StderrCompleted;

        /// <summary>Only a failed start or confirmed root exit with both streams drained permits ordinary lease cleanup.</summary>
        public bool CleanupSafe => !Started || (ExitConfirmed && PipesCompleted);

        /// <summary>Stable evidence classification; incomplete streams remain unsafe even after a confirmed root exit.</summary>
        public string TerminationState => !Started ? "not-started" : !ExitConfirmed ? "unproven-exit"
            : !PipesCompleted ? "unfinished-pipes" : "confirmed-exit";
        /// <summary>True only when parent supervision exceeded the declared process lifetime.</summary>
        public bool TimedOut
        {
            get; set;
        }
        /// <summary>Cancellation observed during supervision; CleanupSafe separately records whether cleanup completed.</summary>
        public bool Cancelled
        {
            get; set;
        }

        /// <summary>Actual bytes written to the separate stdout evidence sink.</summary>
        public long StdoutBytes
        {
            get; set;
        }

        /// <summary>Actual bytes written to the separate stderr evidence sink.</summary>
        public long StderrBytes
        {
            get; set;
        }
        /// <summary>Durable combined pipe log; separate stdout/stderr sinks retain channel identity.</summary>
        public string LogFile
        {
            get; set;
        }
        /// <summary>Measured child launch wall time, excluding input preparation.</summary>
        public TimeSpan Duration
        {
            get; set;
        }
        /// <summary>Elapsed time to the first stdout driver category, or null for a driverless process.</summary>
        public double? BootSeconds
        {
            get; set;
        }
        /// <summary>Sampled root-process CPU time in seconds; unavailable values remain null.</summary>
        public double? CpuSeconds
        {
            get; set;
        }
        /// <summary>Largest sampled root-process working-set peak in MiB; unavailable values remain null.</summary>
        public double? PeakMemoryMb
        {
            get; set;
        }
        /// <summary>Number of captured log lines across both pipes.</summary>
        public long LogLines
        {
            get; set;
        }
        /// <summary>Original merged callback lines used by strict result parsing, without discarding stderr diagnostics.</summary>
        public List<string> Output { get; set; } = new();

        /// <summary>Joined original callback lines for diagnostics and toolchain identity checks.</summary>
        public string OutputText => string.Join(Environment.NewLine, Output);
    }
}
