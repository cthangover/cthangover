namespace Cthangover.TestRunner
{
    /// <summary>Measured runner phases; concurrent work can overlap. Restore counts actual scans, not component starts.</summary>
    public sealed class RunPhases
    {
        /// <summary>Physical project snapshots; component data homes do not count as copies.</summary>
        public int Copies { get; set; }
        /// <summary>Child wall duration summed per project lane, excluding preparation/restore/collection.</summary>
        public Dictionary<string, double> LaneBusySeconds { get; set; } = new();
        /// <summary>Wall interval from first launch preparation to final collection; later copy preparation may overlap it.</summary>
        public double LaunchSeconds { get; set; }
        /// <summary>Sum of test-child durations divided by launch-phase wall time.</summary>
        public double Parallelism { get; set; }
        /// <summary>Initial build subprocess time; also exposed separately from test child durations.</summary>
        public double BuildSeconds { get; set; }
        /// <summary>Physical snapshot construction, including baseline preparation.</summary>
        public double WorkspaceCopySeconds { get; set; }
        /// <summary>Total project restoration time, including a failed restoration attempt.</summary>
        public double RestoreSeconds { get; set; }
        /// <summary>Number of project restoration attempts; the first component does not restore.</summary>
        public int RestoreCount { get; set; }
        /// <summary>Copying logs, sandboxes, images and failed project changes into durable artifacts.</summary>
        public double ArtifactCollectionSeconds { get; set; }
        /// <summary>Measured report checkpoints/exports; the final JSON snapshot excludes its own last write.</summary>
        public double ReportWritingSeconds { get; set; }
        /// <summary>Disposal of owned workspace/user/temp directories, or the retention decision.</summary>
        public double CleanupSeconds { get; set; }
    }
}
