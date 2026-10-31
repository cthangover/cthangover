namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    /// </summary>
    public static class ModDownloader
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static readonly HashSet<string> _installingMods = new();

        /// <summary>Fired on every progress report; carries the stage, fraction and message.</summary>
        public static event Action<InstallProgress> OnProgress;
        /// <summary>Fired once when an install finishes; <c>true</c> on success, <c>false</c> on any failure.</summary>
        public static event Action<string, bool> OnCompleted;

        /// <summary>Asks the active UI for a session password after an encrypted download cannot be opened. Cancellation aborts installation.</summary>
        public static event Func<string, Task<string>> PasswordRequired;

        /// <summary>
        /// Installs a mod from the catalog into the user mods root. Selects the
        /// entry (optionally from a specific source catalog), picks the download
        /// matching <paramref name="version"/> (or the newest), verifies the
        /// archive hash, extracts through a staging directory, and swaps it into
        /// place. Returns <c>false</c> and reports the reason via
        /// <see cref="OnCompleted"/>(false) on any failure; concurrent installs
        /// of the same mod are rejected.
        /// </summary>
        /// <param name="modId">Canonical mod id to install.</param>
        /// <param name="version">Specific version to install, or null for the newest download.</param>
        /// <param name="sourceUrl">Catalog URL that published the entry (disambiguates variant conflicts), or null.</param>
        /// <param name="password">Optional ZIP/7z password, used for this installation only.</param>
		public static async Task<bool> InstallAsync(string modId, string version = null, string sourceUrl = null, string password = null)
		{
			if (string.IsNullOrEmpty(modId))
				return Fail(modId, "Mod ID is empty");

            lock (_installingMods)
                if (!_installingMods.Add(modId))
                    return false;

			try
			{
				CatalogService.LoadCache();
				var cached = CatalogService.CachedEntries;
				if (cached == null)
					return Fail(modId, "Catalog not loaded");

				var entry = CatalogMerger.FindEntry(cached, modId, sourceUrl);
				if (entry == null)
					return Fail(modId, "Mod not found in catalog");

				if (entry.Downloads == null || entry.Downloads.Count == 0)
					return Fail(modId, "No download URLs available");

				var download = FindDownloadForVersion(entry.Downloads, version)
					?? entry.Downloads[0];

                var tempFile = Path.Combine(
                    Path.GetTempPath(), $"cthangover_dl_{modId}_{Guid.NewGuid():N}.package");

                try
                {
                    if (!await DownloadWithProgressAsync(download.Urls, tempFile))
                        return Fail(modId, "All download mirrors failed");

                    ReportProgress(InstallStep.VerifyingArchive, 0, "Verifying and preparing package...");
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
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (OperationCanceledException)
            {
                ReportProgress(InstallStep.Failed, 0, "Installation cancelled");
                OnCompleted?.Invoke(modId, false);
                return false;
            }
            catch (Exception ex)
            {
                return Fail(modId, ex.Message);
            }
            finally
            {
                _installingMods.Remove(modId);
            }

            ReportProgress(InstallStep.Complete, 1,
                "Install complete. Restart required.");
            OnCompleted?.Invoke(modId, true);
            return true;
        }

        private static void InstallDownloadedPackage(object state)
        {
            var (path, target, id, download, password) = ((string, string, string, CatalogEntryDownload, string))state;
            ModPackageInstaller.Install(path, target, id, download.ArchiveSha256, download.InstallSha256, password);
        }

        private static async Task<bool> DownloadWithProgressAsync(
            List<string> urls, string destinationPath)
        {
            foreach (var url in urls)
            {
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                try
                {
                    GameLogger.Log("DOWNLOADER",
                        $"Downloading '{url}'...");

                    using var response = await _httpClient.GetAsync(
                        url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(destinationPath);

                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progress = Math.Min((float)totalRead / totalBytes, 1f);
                            ReportProgress(InstallStep.Downloading, progress,
                                $"Downloading... {progress * 100:F0}%");
                        }
                        else
                        {
                            ReportProgress(InstallStep.Downloading, 0,
                                $"Downloading... {totalRead / 1024} KB");
                        }
                    }

                    GameLogger.Log("DOWNLOADER",
                        $"Downloaded {totalRead} bytes from '{url}'");
                    return true;
                }
                catch (Exception ex)
                {
                    GameLogger.Log("DOWNLOADER",
                        $"Download from '{url}' failed: {ex.Message}", LogLevel.Warning);
                }
            }

            return false;
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

        private static bool Fail(string modId, string reason)
        {
            ReportProgress(InstallStep.Failed, 0, reason);
            GameLogger.Log("DOWNLOADER",
                $"Failed to install mod '{modId}': {reason}", LogLevel.Error);
            OnCompleted?.Invoke(modId, false);
            return false;
        }

		private static void ReportProgress(InstallStep step, float progress, string message)
		{
			OnProgress?.Invoke(new InstallProgress
			{
				Step = step,
				Progress = progress,
				Message = message,
			});
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
