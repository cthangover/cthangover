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
        public double TotalSeconds => ((FinishedAt ?? DateTimeOffset.UtcNow) - StartedAt).TotalSeconds;
        public string Workspace { get; set; }
        public bool WorkspaceRetained { get; set; }
        public List<string> UserDirectories { get; set; } = new();
        public TestResult Build { get; set; }
        public List<TestResult> Results { get; set; } = new();
    }
}
