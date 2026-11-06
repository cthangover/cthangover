namespace Cthangover.Core.Mods
{
    /// <summary>Snapshot of the downloader state at one moment of an install.</summary>
    public class InstallProgress
    {
        /// <summary>Current pipeline stage.</summary>
        public InstallStep Step { get; set; }
        /// <summary>Completion fraction 0–1; 0 for stages without a measurable fraction.</summary>
        public float Progress { get; set; }
        /// <summary>Human-readable status line for the UI (e.g. "Downloading... 42%").</summary>
        public string Message { get; set; }
        /// <summary>Mod being installed.</summary>
        public string ModId { get; set; }
        /// <summary>Archive bytes on disk so far.</summary>
        public long ReceivedBytes { get; set; }
        /// <summary>Archive size, or 0 when the server did not tell it.</summary>
        public long TotalBytes { get; set; }
        /// <summary>Current speed over the last seconds; 0 while paused or reconnecting.</summary>
        public double BytesPerSecond { get; set; }
        /// <summary>Paused by the player; the partial file is kept.</summary>
        public bool IsPaused { get; set; }
        /// <summary>Failed connection attempts in a row; 0 while the transfer works.</summary>
        public int Retry { get; set; }
    }

}
