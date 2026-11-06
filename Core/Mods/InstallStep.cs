namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Stages of a <see cref="ModDownloader.InstallAsync"/> run, reported
    /// through <see cref="ModDownloader.OnProgress"/> so the UI can render a
    /// progress bar with a human-readable message per stage.
    /// </summary>
    public enum InstallStep
    {
        /// <summary>Waiting for its turn: one download runs at a time (<see cref="DownloadQueue"/>).</summary>
        Queued,
        /// <summary>Fetching the archive from a catalog mirror.</summary>
        Downloading,
        /// <summary>Comparing the downloaded archive against <c>archive_sha256</c>.</summary>
        VerifyingArchive,
        /// <summary>Extracting the archive into a staging directory.</summary>
        Extracting,
        /// <summary>Recomputing the install hash of the extracted mod.</summary>
        VerifyingInstall,
        /// <summary>Mod installed; a restart is required to load it.</summary>
        Complete,
        /// <summary>Install aborted — see the reported message for the reason.</summary>
        Failed,
    }

}
