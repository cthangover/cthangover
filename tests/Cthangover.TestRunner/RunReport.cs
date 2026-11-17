namespace Cthangover.TestRunner
{
    /// <summary>Versioned run envelope; partial reports deliberately cannot claim success.</summary>
    public class RunReport
    {
        /// <summary>Versioned result envelope; additive preparation metrics carry their own schema versions.</summary>
        public int SchemaVersion { get; set; } = 2;
        /// <summary>Unique invocation identity allocated before any output or child process.</summary>
        public string RunId
        {
            get; set;
        }
        /// <summary>UTC invocation start, including planning and preparation overhead.</summary>
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>UTC finalization boundary; null means the report is still partial.</summary>
        public DateTimeOffset? FinishedAt
        {
            get; set;
        }
        /// <summary>True only after coordinated completion; partial checkpoints cannot claim success.</summary>
        public bool Complete
        {
            get; set;
        }
        /// <summary>Requires completed nonempty results, a successful build when present, and every launch contract passing.</summary>
        public bool Success
        {
            get
            {
                if (!Complete || (Build != null && !Build.Passed) || Results.Count == 0)
                {
                    return false;
                }
                foreach (var result in Results)
                {
                    if (!result.Passed)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
        /// <summary>Elapsed run wall time, including runner overhead and initial build; final persistence is a snapshot boundary.</summary>
        public double WallSeconds => ((FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt).TotalSeconds;
        /// <summary>Test subprocess durations, excluding the separately measured initial build.</summary>
        public double ChildSeconds
        {
            get
            {
                var seconds = 0.0;
                foreach (var result in Results)
                {
                    seconds += result.DurationSeconds;
                }
                return seconds;
            }
        }
        /// <summary>Runner phase totals; gaps include planning, process-result parsing and ordinary coordination.</summary>
        public RunPhases Phases { get; set; } = new();
        /// <summary>Actual preparation transfers and checked source/build identity, including refusals.</summary>
        public InputPreparationMetrics InputPreparation
        {
            get; set;
        }

        /// <summary>Prepared-read and bounded persistent-write lane transfer records.</summary>
        public List<WorkspacePreparationMetrics> WorkspaceMetrics { get; set; } = new();

        /// <summary>Scoped retention evidence, including live leases that prohibited deletion.</summary>
        public List<InputRetentionDecision> RetentionDecisions { get; set; } = new();
        /// <summary>Readable summary of actual prepared and persistent-write project roots.</summary>
        public string Workspace
        {
            get; set;
        }
        /// <summary>All physical copies, including independently retained failed lanes.</summary>
        public List<string> Workspaces { get; set; } = new();
        /// <summary>Subset still present after cleanup, including all copies on an interrupted run.</summary>
        public List<string> RetainedWorkspaces { get; set; } = new();
        /// <summary>Whether run-owned diagnostic state survived cleanup; reusable input storage is recorded separately.</summary>
        public bool WorkspaceRetained
        {
            get; set;
        }
        /// <summary>Component data homes retained as provenance for private saves/settings.</summary>
        public List<string> UserDirectories { get; set; } = new();
        /// <summary>Optional measured build outcome, separate from test assertions and readiness.</summary>
        public TestResult Build
        {
            get; set;
        }
        /// <summary>Plan-ordered launch outcomes plus explicitly reported infrastructure violations.</summary>
        public List<TestResult> Results { get; set; } = new();
    }
}
