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
    }

}
