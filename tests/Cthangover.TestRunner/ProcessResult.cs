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
        public TimeSpan Duration { get; set; }
        public List<string> Output { get; set; } = new();

        public string OutputText => string.Join(Environment.NewLine, Output);
    }
}
