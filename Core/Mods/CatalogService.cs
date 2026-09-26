using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Fetches, caches, and merges online mod catalogs from the URLs
    /// configured in <see cref="ModConfig.CatalogUrls"/>. Multiple
    /// catalogs are merged via deduplication by mod ID — the entry
    /// with the highest <see cref="CatalogExportEntry.Version"/> wins.
    ///
    /// Two-tier caching: an on-disk JSON cache at
    /// <c>user://catalog_cache.json</c> avoids network round-trips
    /// on every open of the Network tab; the cache is loaded eagerly
    /// on first access and refreshed when the user presses the
    /// Refresh button or when <see cref="ModConfig.CatalogUrls"/>
    /// changes.
    ///
    /// <see cref="OnRefreshed"/> / <see cref="OnRefreshFailed"/> fire
    /// on the main thread after an async fetch completes so that UI
    /// subscribers can safely rebuild their node trees.
    /// </summary>
    public static class CatalogService
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static List<CatalogExportEntry> _cachedEntries;
        private static bool _isFetching;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan FetchCooldown = TimeSpan.FromSeconds(5);

        /// <summary>The current merged catalog data, or null until first load.</summary>
        public static List<CatalogExportEntry> CachedEntries => _cachedEntries;

        /// <summary>True while an async refresh is in flight.</summary>
        public static bool IsFetching => _isFetching;

        /// <summary>Fired on the main thread after a successful refresh.</summary>
        public static event Action OnRefreshed;

        /// <summary>Fired on the main thread with an error message when refresh fails.</summary>
        public static event Action<string> OnRefreshFailed;

        /// <summary>
        /// Loads cached catalog data from disk if available. Safe to
        /// call multiple times — subsequent calls are no-ops.
        /// </summary>
        public static void LoadCache()
        {
            if (_cachedEntries != null)
                return;

            var path = GetCachePath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    _cachedEntries = JsonSerializer.Deserialize<List<CatalogExportEntry>>(json, JsonOptionsProvider.ReadOptions);
                    GameLogger.Log("CATALOG", $"Loaded catalog cache: {_cachedEntries?.Count ?? 0} entr(y/ies)");
                }
                catch (Exception ex)
                {
                    GameLogger.Log("CATALOG", $"Failed to load catalog cache: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// Fetches all configured catalog URLs, merges the results, and
        /// fires <see cref="OnRefreshed"/> on the main thread. Clears
        /// the on-disk cache if <paramref name="forceRefresh"/> is true
        /// before fetching.
        ///
        /// Cancelling <paramref name="cancellationToken"/> aborts the
        /// in-flight HTTP requests and suppresses the completion events, so a
        /// caller whose window is closing is never called back. The
        /// <see cref="IsFetching"/> flag is always cleared — on success,
        /// cancellation, and unexpected exceptions alike — otherwise a single
        /// failed merge would permanently block every later refresh.
        /// </summary>
        public static async Task RefreshAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (_isFetching)
                return;

            var sinceLast = DateTime.UtcNow - _lastFetchTime;
            if (!forceRefresh && sinceLast < FetchCooldown)
            {
                var remain = (FetchCooldown - sinceLast).TotalSeconds;
                GameLogger.Log("CATALOG",
                    $"Skipping refresh (cooldown, last attempt was {sinceLast.TotalSeconds:F1}s ago, {remain:F1}s remaining)");
                return;
            }

            _isFetching = true;
            _lastFetchTime = DateTime.UtcNow;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (forceRefresh)
                {
                    ClearCache();
                    ModIconCache.Clear();
                }

                var allEntries = new List<CatalogExportEntry>();
                var errors = new List<string>();
                var urls = ModConfig.Instance.CatalogUrls;

                if (urls == null || urls.Count == 0)
                {
                    _cachedEntries = new List<CatalogExportEntry>();
                    OnRefreshed?.Invoke();
                    return;
                }

                GameLogger.Log("CATALOG", $"Starting catalog refresh from {urls.Count} URL(s)...");

                foreach (var url in urls)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    try
                    {
                        GameLogger.Log("CATALOG", $"Fetching catalog from '{url}'...");
                        var json = await _httpClient.GetStringAsync(url, cancellationToken);
                        var catalog = JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptionsProvider.ReadOptions);
                        if (catalog?.Mods != null)
                        {
                            foreach (var mod in catalog.Mods)
                                CatalogMerger.TagSource(mod, url);
                            allEntries.AddRange(catalog.Mods);
                            GameLogger.Log("CATALOG", $"Fetched {catalog.Mods.Count} entr(y/ies) from '{url}'");
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to fetch catalog from '{url}': {ex.Message}";
                        GameLogger.Log("CATALOG", msg, LogLevel.Error);
                        errors.Add(msg);
                    }
                }

                if (allEntries.Count > 0)
                {
                    _cachedEntries = CatalogMerger.MergeAndDeduplicate(allEntries);
                    SaveCache(_cachedEntries);
                    GameLogger.Log("CATALOG", $"Catalog refresh complete: {_cachedEntries.Count} total entr(y/ies) merged");
                    OnRefreshed?.Invoke();
                }
                else if (_cachedEntries == null && errors.Count > 0)
                {
                    GameLogger.Log("CATALOG", "Catalog refresh failed: all URLs returned errors", LogLevel.Error);
                    OnRefreshFailed?.Invoke(string.Join("\n", errors));
                }
                else
                {
                    GameLogger.Log("CATALOG", "Catalog refresh complete: no new entries");
                    OnRefreshed?.Invoke();
                }
            }
            finally
            {
                _isFetching = false;
            }
        }

        /// <summary>
        /// Deletes the on-disk cache and clears in-memory state.
        /// The next call to <see cref="RefreshAsync"/> will fetch
        /// everything from scratch.
        /// </summary>
        public static void ClearCache()
        {
            _cachedEntries = null;
            var path = GetCachePath();
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// Compares a locally-installed mod against its catalog entry.
        /// </summary>
        /// <param name="localVersion">Installed mod version string.</param>
        /// <param name="localInstallSha256">SHA256 of the installed mod's content,
        /// computed by <see cref="ModHasher.ComputeInstallSha256"/>.</param>
        /// <param name="remoteEntry">Corresponding catalog entry.</param>
        /// <returns>The relationship between the local copy and the remote version.</returns>
        public static RemoteModStatus GetRemoteModStatus(
            string localVersion, string localInstallSha256, CatalogExportEntry remoteEntry)
        {
            if (localVersion == null)
                return RemoteModStatus.NotInstalled;

            var download = FindDownloadForVersion(remoteEntry, localVersion);
            var remoteVersion = remoteEntry.Version;
            if (!string.IsNullOrEmpty(remoteVersion) && VersionHelper.IsGreaterThan(remoteVersion, localVersion))
                return RemoteModStatus.UpdateAvailable;

            if (!string.IsNullOrEmpty(localInstallSha256)
                && download != null
                && !string.IsNullOrEmpty(download.InstallSha256)
                && !string.Equals(localInstallSha256, download.InstallSha256, StringComparison.OrdinalIgnoreCase))
                return RemoteModStatus.ModifiedLocally;

            return RemoteModStatus.Installed;
        }

        private static CatalogEntryDownload FindDownloadForVersion(CatalogExportEntry entry, string version)
        {
            if (entry?.Downloads == null)
                return null;
            foreach (var d in entry.Downloads)
            {
                if (string.Equals(d.Version, version, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }


        private static void SaveCache(List<CatalogExportEntry> entries)
        {
            try
            {
                var path = GetCachePath();
                var json = JsonSerializer.Serialize(entries, JsonOptionsProvider.WriteIndentedOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                GameLogger.Log("CATALOG", $"Failed to save catalog cache: {ex.Message}", LogLevel.Warning);
            }
        }

        private static string GetCachePath()
        {
            if (GameEnvironment.IsRuntime)
                return ProjectSettings.GlobalizePath("user://catalog_cache.json");
            return "catalog_cache.json";
        }
    }

    /// <summary>
    /// Relationship between a locally-installed mod and its remote
    /// catalog counterpart.
    /// </summary>
    public enum RemoteModStatus
    {
        /// <summary>The mod is installed and up-to-date with the catalog.</summary>
        Installed,

        /// <summary>A newer version is available in the catalog.</summary>
        UpdateAvailable,

        /// <summary>The mod is not installed locally.</summary>
        NotInstalled,

        /// <summary>Same version, but the local content differs from the
        /// catalog's <c>install_sha256</c> — the player has edited the
        /// mod locally and overwriting would lose their changes.</summary>
        ModifiedLocally,
    }
}
