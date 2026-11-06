namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Downloads and installs mods from the online catalog into the user mods
    /// root (<c>user://mods/</c>). One <see cref="InstallAsync"/> run picks the
    /// catalog entry (by id and, when given, the source catalog that published
    /// it), streams the ZIP/7z from the first reachable mirror, verifies
    /// <c>archive_sha256</c>, extracts through a staging directory (flattening
    /// a single wrapper folder), verifies <c>install_sha256</c>, and swaps the
    /// result into place. The new mod set only loads after a restart — like
    /// every other mod-set change.
    ///
    /// Unstable connections are normal operation. A broken or stalled transfer
    /// resumes with <c>Range</c> from the bytes already received, rotating
    /// mirrors and waiting per <see cref="TransferRetry"/>; expired private links
    /// are renewed by refreshing the catalog. When the archive hash is known the
    /// partial file is kept under that hash, so a later attempt or a game restart
    /// continues it. Slowness alone never aborts a download.
    /// </summary>
    public static class ModDownloader
    {
        // No overall timeout: a 200 MiB archive on a slow link may take hours.
        // Each read has its own stall timer (TransferRetry.StallTimeout).
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static readonly HashSet<string> _installingMods = new();

        // One download at a time, first come first served: a player holds one
        // connection to a server, and queued mods arrive in the chosen order.
        private static readonly DownloadQueue _queue = new();
        private static readonly Dictionary<string, Install> _active = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Message of the final <see cref="InstallStep.Failed"/> report of a cancelled install.</summary>
        public const string CancelledMessage = "Installation cancelled";

        /// <summary>Progress events are coalesced to this interval (stage changes always pass).</summary>
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>Fired on every progress report; carries the stage, fraction and message.</summary>
        public static event Action<InstallProgress> OnProgress;
        /// <summary>Fired once when an install finishes; <c>true</c> on success, <c>false</c> on any failure.</summary>
        public static event Action<string, bool> OnCompleted;

        /// <summary>Asks the active UI for a session password after an encrypted download cannot be opened. Cancellation aborts installation.</summary>
        public static event Func<string, Task<string>> PasswordRequired;

        /// <summary>Latest progress of every unfinished install, queued ones included, in start order.</summary>
        public static IReadOnlyList<InstallProgress> Active
        {
            get
            {
                lock (_active)
                    return _active.Values.OrderBy(install => install.Order).Select(install => install.Last).Where(last => last != null).ToList();
            }
        }

        /// <summary>Pauses the mod's download; the received part is kept. Only the downloading stage can pause.</summary>
        public static void Pause(string modId)
        {
            var install = Find(modId);
            if (install == null || install.Last?.Step != InstallStep.Downloading || !install.Control.Pause())
                return;
            install.Speed.Reset();
            Report(install, InstallStep.Downloading, install.Last.Progress, "Paused", install.Last.ReceivedBytes, install.Last.TotalBytes, 0, force: true);
        }

        /// <summary>Continues a paused download from where it stopped.</summary>
        public static void Resume(string modId)
        {
            var install = Find(modId);
            if (install == null || !install.Control.Resume())
                return;
            Report(install, InstallStep.Downloading, install.Last?.Progress ?? 0, "Resuming...", install.Last?.ReceivedBytes ?? 0, install.Last?.TotalBytes ?? 0, 0, force: true);
        }

        /// <summary>Cancels a queued, paused or downloading install; its partial file is removed.</summary>
        public static void Cancel(string modId) => Find(modId)?.Control.Cancel();

        private static Install Find(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;
            lock (_active)
                return _active.TryGetValue(modId, out var install) ? install : null;
        }

        /// <summary>
        /// Installs a mod from the catalog into the user mods root. Waits for its
        /// turn in the download queue, refreshes private links, picks the download
        /// matching <paramref name="version"/> (or the newest), downloads it with
        /// resume, verifies the archive hash, extracts through a staging directory,
        /// and swaps it into place. <see cref="Pause"/>, <see cref="Resume"/> and
        /// <see cref="Cancel"/> drive it from the UI. Returns <c>false</c> and
        /// reports the reason via <see cref="OnCompleted"/>(false) on any failure
        /// or cancellation; concurrent installs of the same mod are rejected.
        /// </summary>
        /// <param name="modId">Canonical mod id to install.</param>
        /// <param name="version">Specific version to install, or null for the newest download.</param>
        /// <param name="sourceUrl">Catalog URL that published the entry (disambiguates variant conflicts), or null.</param>
        /// <param name="password">Optional ZIP/7z password, used for this installation only.</param>
        public static async Task<bool> InstallAsync(string modId, string version = null, string sourceUrl = null, string password = null)
        {
            if (string.IsNullOrEmpty(modId))
                return Fail(null, modId, "Mod ID is empty");

            lock (_installingMods)
                if (!_installingMods.Add(modId))
                    return false;

            var install = new Install(modId);
            lock (_active)
                _active[modId] = install;
            try
            {
                Report(install, InstallStep.Queued, 0, "Waiting for the previous download...", 0, 0, 0, force: true);
                CatalogEntryDownload download;
                string tempFile;
                var downloaded = false;
                using (await _queue.EnterAsync(install.Control.Cancellation))
                {
                    CatalogService.LoadCache();
                    // Temporary links and permissions are refreshed immediately before
                    // a private download, including after expiry or revocation.
                    if (ModConfig.Instance.CatalogTokenFiles.Count > 0)
                    {
                        while (CatalogService.IsFetching)
                            await Task.Delay(25, install.Control.Cancellation);
                        await CatalogService.RefreshAsync(forceRefresh: true, install.Control.Cancellation);
                    }
                    var cached = CatalogService.CachedEntries;
                    if (cached == null)
                        return Fail(install, modId, "Catalog not loaded");

                    var entry = CatalogMerger.FindEntry(cached, modId, sourceUrl);
                    if (entry == null)
                        return Fail(install, modId, "Mod not found in catalog");

                    if (entry.Downloads == null || entry.Downloads.Count == 0)
                        return Fail(install, modId, "No download URLs available");

                    download = FindDownloadForVersion(entry.Downloads, version) ?? entry.Downloads[0];
                    tempFile = PartialPath(modId, download);
                    Report(install, InstallStep.Downloading, 0, "Connecting...", 0, download.SizeBytes, 0, force: true);
                    try
                    {
                        if (!await ResumableDownload.RunAsync(_httpClient, download, tempFile,
                                () => RenewUrlsAsync(modId, sourceUrl, download),
                                (received, total, retry) => ReportDownload(install, received, total, retry), install.Control))
                            return Fail(install, modId, "Download failed: no connection to any mirror");
                        downloaded = true;
                    }
                    finally
                    {
                        // A cancelled download leaves nothing behind; a failed one keeps
                        // its hash-named partial for the next attempt.
                        if (!downloaded && File.Exists(tempFile) && (install.Control.IsCancelled || !IsResumable(download)))
                            File.Delete(tempFile);
                    }
                }

                // The next queued download starts while this archive is installed.
                try
                {
                    Report(install, InstallStep.VerifyingArchive, 0, "Verifying and preparing package...", download.SizeBytes, download.SizeBytes, 0, force: true);
                    var targetDir = GetModTargetDir(modId);
                    var packagePassword = password;
                    while (true)
                    {
                        try
                        {
                            await Task.Factory.StartNew(InstallDownloadedPackage,
                                (tempFile, targetDir, modId, download, packagePassword),
                                System.Threading.CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                            break;
                        }
                        catch (SharpCompress.Common.CryptographicException)
                        {
                            var request = PasswordRequired;
                            if (request == null)
                                throw;
                            packagePassword = await request(modId);
                            if (packagePassword == null)
                                throw new OperationCanceledException("Password entry cancelled");
                        }
                    }

                    GameLogger.Log("DOWNLOADER",
                        $"Successfully installed mod '{modId}' to '{targetDir}'");
                }
                finally
                {
                    // Complete bytes are removed whether or not they installed.
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (OperationCanceledException)
            {
                GameLogger.Log("DOWNLOADER", $"Installation of '{modId}' cancelled");
                Report(install, InstallStep.Failed, 0, CancelledMessage, 0, 0, 0, force: true);
                OnCompleted?.Invoke(modId, false);
                return false;
            }
            catch (Exception ex)
            {
                return Fail(install, modId, ex.Message);
            }
            finally
            {
                lock (_installingMods)
                    _installingMods.Remove(modId);
                lock (_active)
                    _active.Remove(modId);
            }

            Report(install, InstallStep.Complete, 1, "Install complete. Restart required.", 0, 0, 0, force: true);
            OnCompleted?.Invoke(modId, true);
            return true;
        }

        /// <summary>State of one unfinished install: its controls, speed meter and last report.</summary>
        private sealed class Install
        {
            private static long _counter;

            public Install(string modId)
            {
                ModId = modId;
                Order = Interlocked.Increment(ref _counter);
            }

            public string ModId { get; }
            public long Order { get; }
            public DownloadControl Control { get; } = new();
            public TransferSpeedMeter Speed { get; } = new();
            public Stopwatch Clock { get; } = Stopwatch.StartNew();
            public TimeSpan LastReport { get; set; } = TimeSpan.MinValue;
            public InstallProgress Last { get; set; }
        }

        private static void InstallDownloadedPackage(object state)
        {
            var (path, target, id, download, password) = ((string, string, string, CatalogEntryDownload, string))state;
            ModPackageInstaller.Install(path, target, id, download.ArchiveSha256, download.InstallSha256, password);
        }

        private static bool IsResumable(CatalogEntryDownload download) =>
            download.ArchiveSha256 is { Length: 64 } hash && hash.All(Uri.IsHexDigit);

        /// <summary>
        /// A partial download named by its archive hash is resumable across
        /// attempts and restarts: the hash check after download rejects any mix of
        /// different bytes. Without a hash every attempt starts from scratch.
        /// </summary>
        private static string PartialPath(string modId, CatalogEntryDownload download)
        {
            if (!IsResumable(download))
                return Path.Combine(Path.GetTempPath(), $"cthangover_dl_{modId}_{Guid.NewGuid():N}.package");
            var directory = Path.Combine(Path.GetTempPath(), "cthangover_downloads");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, download.ArchiveSha256.ToLowerInvariant() + ".part");
        }

        /// <summary>
        /// Private links expire after hours; a long or often interrupted download
        /// asks the catalog for fresh links to the same bytes (same version and hash).
        /// </summary>
        private static async Task<List<string>> RenewUrlsAsync(string modId, string sourceUrl, CatalogEntryDownload download)
        {
            if (ModConfig.Instance.CatalogTokenFiles.Count == 0)
                return null;
            while (CatalogService.IsFetching)
                await Task.Delay(25);
            await CatalogService.RefreshAsync(forceRefresh: true);
            var entry = CatalogMerger.FindEntry(CatalogService.CachedEntries, modId, sourceUrl);
            var renewed = FindDownloadForVersion(entry?.Downloads, download.Version);
            if (renewed == null || !string.Equals(renewed.ArchiveSha256, download.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                return null;
            return renewed.Urls;
        }

        private static void ReportDownload(Install install, long received, long total, int retry)
        {
            if (total <= 0)
                total = 0;
            var fraction = total > 0 ? Math.Min((float)received / total, 1f) : 0;
            if (install.Control.IsPaused)
                return;
            if (retry > 0)
            {
                install.Speed.Reset();
                Report(install, InstallStep.Downloading, fraction, $"Connection lost, retrying ({retry}/{TransferRetry.TransferFailures})...",
                    received, total, 0, force: true, retry: retry);
                return;
            }
            var speed = install.Speed.Add(install.Clock.Elapsed, received);
            var message = total > 0 ? $"Downloading... {fraction * 100:F0}%" : $"Downloading... {received / 1024} KB";
            Report(install, InstallStep.Downloading, fraction, message, received, total, speed, force: total > 0 && received >= total);
        }

        private static string GetModTargetDir(string modId)
        {
            if (modId != Path.GetFileName(modId) || modId.Contains('\\') || modId == "." || modId == "..")
                throw new ArgumentException("Invalid mod id", nameof(modId));
            if (GameEnvironment.IsRuntime)
            {
                var modsRoot = ProjectSettings.GlobalizePath("user://mods/");
                if (!Directory.Exists(modsRoot))
                    Directory.CreateDirectory(modsRoot);
                return Path.Combine(modsRoot, modId);
            }

            return Path.Combine("mods", modId);
        }

        private static bool Fail(Install install, string modId, string reason)
        {
            Report(install, InstallStep.Failed, 0, reason, 0, 0, 0, force: true);
            GameLogger.Log("DOWNLOADER",
                $"Failed to install mod '{modId}': {reason}", LogLevel.Error);
            OnCompleted?.Invoke(modId, false);
            return false;
        }

        /// <summary>Records and publishes one report; frequent download reports are coalesced.</summary>
        private static void Report(Install install, InstallStep step, float progress, string message,
            long received, long total, double speed, bool force = false, int retry = 0)
        {
            var report = new InstallProgress
            {
                Step = step, Progress = progress, Message = message, ModId = install?.ModId,
                ReceivedBytes = received, TotalBytes = total, BytesPerSecond = speed,
                IsPaused = install?.Control.IsPaused == true, Retry = retry,
            };
            if (install != null)
            {
                var now = install.Clock.Elapsed;
                var changed = install.Last == null || install.Last.Step != step || install.Last.IsPaused != report.IsPaused;
                install.Last = report;
                if (!force && !changed && now - install.LastReport < ProgressInterval)
                    return;
                install.LastReport = now;
            }
            OnProgress?.Invoke(report);
        }

		private static CatalogEntryDownload FindDownloadForVersion(
			List<CatalogEntryDownload> downloads, string version)
		{
			if (downloads == null || string.IsNullOrEmpty(version))
				return null;
			foreach (var d in downloads)
				if (string.Equals(d.Version, version, StringComparison.OrdinalIgnoreCase))
					return d;
			return null;
		}
	}
}
