namespace Cthangover.TestRunner
{
    /// <summary>
    /// Captured outcome of an external process: merged stdout/stderr lines
    /// (Godot writes its logs to stdout, MSBuild errors to stderr), exit code,
    /// duration and whether the timeout had to kill it.
    /// </summary>
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string LogFile { get; set; }
        public TimeSpan Duration { get; set; }
        /// <summary>Elapsed time to the first stdout driver category, or null for a driverless process.</summary>
        public double? BootSeconds { get; set; }
        /// <summary>Sampled root-process CPU time in seconds; unavailable values remain null.</summary>
        public double? CpuSeconds { get; set; }
        /// <summary>Largest sampled root-process working-set peak in MiB; unavailable values remain null.</summary>
        public double? PeakMemoryMb { get; set; }
        /// <summary>Number of captured log lines across both pipes.</summary>
        public long LogLines { get; set; }
        public List<string> Output { get; set; } = new();

        public string OutputText => string.Join(Environment.NewLine, Output);
    }
}
