namespace Cthangover.TestRunner
{
    /// <summary>Durable owner identity survives SIGKILL; process start time prevents PID reuse from protecting stale copies.</summary>
    public sealed class WorkspaceOwnerInfo
    {
        public string RunId { get; set; }
        public string GroupId { get; set; }
        public int ProcessId { get; set; }
        public long ProcessStartedTicks { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string ArtifactsDirectory { get; set; }
        public List<string> UserDirectories { get; set; } = new();
    }
}
