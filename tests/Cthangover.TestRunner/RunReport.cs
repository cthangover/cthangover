namespace Cthangover.TestRunner
{
    /// <summary>Versioned run envelope; partial reports deliberately cannot claim success.</summary>
    public class RunReport
    {
        public int SchemaVersion { get; set; } = 2;
        public string RunId { get; set; }
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedAt { get; set; }
        public bool Complete { get; set; }
        public bool Success => Complete && (Build == null || Build.Passed) && Results.Count > 0 && Results.All(r => r.Passed);
        /// <summary>Elapsed run wall time, including runner overhead and initial build; final persistence is a snapshot boundary.</summary>
        public double WallSeconds => ((FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt).TotalSeconds;
        /// <summary>Test subprocess durations, excluding the separately measured initial build.</summary>
        public double ChildSeconds => Results.Sum(result => result.DurationSeconds);
        /// <summary>Runner phase totals; gaps include planning, process-result parsing and ordinary coordination.</summary>
        public RunPhases Phases { get; set; } = new();
        public string Workspace { get; set; }
        /// <summary>All physical copies, including independently retained failed lanes.</summary>
        public List<string> Workspaces { get; set; } = new();
        /// <summary>Subset still present after cleanup, including all copies on an interrupted run.</summary>
        public List<string> RetainedWorkspaces { get; set; } = new();
        public bool WorkspaceRetained { get; set; }
        public List<string> UserDirectories { get; set; } = new();
        public TestResult Build { get; set; }
        public List<TestResult> Results { get; set; } = new();
    }
}
