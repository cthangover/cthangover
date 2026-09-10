using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    public enum InstallStep
    {
        Downloading,
        VerifyingArchive,
        Extracting,
        VerifyingInstall,
        Complete,
        Failed,
    }

    public class InstallProgress
    {
        public InstallStep Step { get; set; }
        public float Progress { get; set; }
        public string Message { get; set; }
    }

    public static class ModDownloader
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static readonly HashSet<string> _installingMods = new();

        public static event Action<InstallProgress> OnProgress;
        public static event Action<string, bool> OnCompleted;

		public static async Task<bool> InstallAsync(string modId, string version = null)
		{
			if (string.IsNullOrEmpty(modId))
				return Fail(modId, "Mod ID is empty");

			if (_installingMods.Contains(modId))
				return false;

			_installingMods.Add(modId);

			try
			{
				CatalogService.LoadCache();
				var cached = CatalogService.CachedEntries;
				if (cached == null)
					return Fail(modId, "Catalog not loaded");

				var entry = cached.FirstOrDefault(e => e.Id == modId);
				if (entry == null)
					return Fail(modId, "Mod not found in catalog");

				if (entry.Downloads == null || entry.Downloads.Count == 0)
					return Fail(modId, "No download URLs available");

				var download = FindDownloadForVersion(entry.Downloads, version)
					?? entry.Downloads[0];

                var tempFile = Path.Combine(
                    Path.GetTempPath(), $"cthangover_dl_{modId}_{Guid.NewGuid():N}.zip");

                try
                {
                    if (!await DownloadWithProgressAsync(download.Urls, tempFile))
                        return Fail(modId, "All download mirrors failed");

                    ReportProgress(InstallStep.VerifyingArchive, 0,
                        $"Verifying archive...");

                    if (!string.IsNullOrEmpty(download.ArchiveSha256))
                    {
                        var archiveHash = ModHasher.ComputeArchiveSha256(tempFile);
                        if (!string.Equals(archiveHash, download.ArchiveSha256,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return Fail(modId,
                                $"Archive SHA256 mismatch: expected {download.ArchiveSha256}, got {archiveHash}");
                        }
                    }

					ReportProgress(InstallStep.Extracting, 0,
						$"Installing {modId}...");

					var targetDir = GetModTargetDir(modId);
					var stagingDir = targetDir + "_staging";

					if (Directory.Exists(stagingDir))
						Directory.Delete(stagingDir, recursive: true);

					ZipFile.ExtractToDirectory(tempFile, stagingDir);

					var stagingEntries = Directory.GetFileSystemEntries(stagingDir);
					if (stagingEntries.Length == 1
						&& Directory.Exists(stagingEntries[0])
						&& !File.Exists(Path.Combine(stagingDir, "manifest.json")))
					{
						var wrapperDir = stagingEntries[0];
						var flatDir = stagingDir + "_flat";
						if (Directory.Exists(flatDir))
							Directory.Delete(flatDir, recursive: true);
						Directory.Move(wrapperDir, flatDir);
						Directory.Delete(stagingDir, recursive: true);
						stagingDir = flatDir;
					}

					if (!File.Exists(Path.Combine(stagingDir, "manifest.json")))
					{
						if (Directory.Exists(stagingDir))
							Directory.Delete(stagingDir, recursive: true);
						return Fail(modId, "Extracted zip does not contain a manifest.json");
					}

					if (Directory.Exists(targetDir))
						Directory.Delete(targetDir, recursive: true);
					Directory.Move(stagingDir, targetDir);

                    ReportProgress(InstallStep.VerifyingInstall, 0,
                        $"Verifying install...");

                    if (!string.IsNullOrEmpty(download.InstallSha256))
                    {
                        var installHash = ModHasher.ComputeInstallSha256(targetDir);
                        if (!string.Equals(installHash, download.InstallSha256,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            GameLogger.Log("DOWNLOADER",
                                $"Install SHA256 mismatch for '{modId}': expected {download.InstallSha256}, got {installHash}",
                                LogLevel.Warning);
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
