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
    /// Private sources use file-backed bearers with redirects disabled; only
    /// public source data is persisted. Every successful response, including an
    /// empty one, replaces the previous access snapshot.
    /// </summary>
    public static class CatalogService
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        // A private source must not redirect the bearer to any other endpoint.
        private static readonly System.Net.Http.HttpClient _privateHttpClient = new(
            new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static List<CatalogExportEntry> _cachedEntries;
        private static List<ModCollection> _cachedCollections;
        private static bool _isFetching;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan FetchCooldown = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Attempts per catalog source. A dropped connection must not blank a
        /// private catalog (and its download links) until the next refresh.
        /// </summary>
        private const int CatalogFailures = 4;

        /// <summary>
        /// Sends a fresh request until a non-transient answer arrives or
        /// <see cref="CatalogFailures"/> attempts failed; the last answer is returned.
        /// </summary>
        private static async Task<System.Net.Http.HttpResponseMessage> SendWithRetryAsync(System.Net.Http.HttpClient client,
            Func<System.Net.Http.HttpRequestMessage> create, CancellationToken cancellation)
        {
            var retry = new TransferRetry(CatalogFailures);
            while (true)
            {
                System.Net.Http.HttpResponseMessage response;
                try
                {
                    using var request = create();
                    response = await client.SendAsync(request, cancellation);
                }
                catch (Exception error) when (TransferRetry.IsTransient(error, cancellation))
                {
                    GameLogger.Log("CATALOG", $"Catalog request interrupted: {error.GetType().Name}; attempt {retry.Failures + 1}", LogLevel.Warning);
                    if (!await retry.WaitAsync(null, cancellation))
                        throw;
                    continue;
                }
                if (!TransferRetry.IsTransient(response.StatusCode) || !await retry.WaitAsync(TransferRetry.RetryAfter(response), cancellation))
                    return response;
                response.Dispose();
            }
        }

        /// <summary>The current merged catalog data, or null until first load.</summary>
        public static List<CatalogExportEntry> CachedEntries => _cachedEntries;

        /// <summary>
        /// The current merged collection list from every catalog, or null until
        /// first load. Consumed by <see cref="ModCollectionStore.LoadAll"/>.
        /// </summary>
        public static List<ModCollection> CachedCollections => _cachedCollections;

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
                    if (TryLoadEnvelope(json))
                    {
                        GameLogger.Log("CATALOG",
                            $"Loaded catalog cache: {_cachedEntries?.Count ?? 0} mod(s), {_cachedCollections?.Count ?? 0} collection(s)");
                        return;
                    }

                    GameLogger.Log("CATALOG", "Catalog cache has an unknown format; it is ignored and refreshed", LogLevel.Warning);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("CATALOG", $"Failed to load catalog cache: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// Reads the cache envelope (<see cref="CatalogCacheRoot"/>). Returns
        /// false when the text is not an envelope object.
        /// </summary>
        private static bool TryLoadEnvelope(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var trimmed = json.TrimStart();
            if (trimmed[0] != '{')
                return false;

            var root = JsonSerializer.Deserialize<CatalogCacheRoot>(json, JsonOptionsProvider.ReadOptions);
            if (root == null)
                return false;

            _cachedEntries = root.Mods ?? new List<CatalogExportEntry>();
            _cachedCollections = root.Collections ?? new List<ModCollection>();
            return true;
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
            var notifyRefreshed = false;
            string refreshErrors = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (forceRefresh)
                {
                    ClearCache();
                    ModIconCache.Clear();
                }

                var allEntries = new List<CatalogExportEntry>();
                var allCollections = new List<ModCollection>();
                var publicEntries = new List<CatalogExportEntry>();
                var publicCollections = new List<ModCollection>();
                var successfulSources = 0;
                var errors = new List<string>();
                var urls = ModConfig.Instance.CatalogUrls;

                if (urls == null || urls.Count == 0)
                {
                    _cachedEntries = new List<CatalogExportEntry>();
                    _cachedCollections = new List<ModCollection>();
                    notifyRefreshed = true;
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
                        var isPrivate = ModConfig.Instance.CatalogTokenFiles.TryGetValue(url, out var tokenFile);
                        if (isPrivate && string.IsNullOrWhiteSpace(tokenFile))
                            throw new InvalidOperationException("Private catalog token file is not configured");
                        using var response = await SendWithRetryAsync(isPrivate ? _privateHttpClient : _httpClient,
                            () => CatalogRequest.Create(url, isPrivate ? tokenFile : null), cancellationToken);
                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var catalog = JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptionsProvider.ReadOptions);
                        if (catalog?.Meta == null || catalog.Meta.Version != 1)
                            throw new InvalidOperationException("Unsupported catalog envelope");
                        successfulSources++;
                        if (catalog?.Mods != null)
                        {
                            foreach (var mod in catalog.Mods)
                                CatalogMerger.TagSource(mod, url);
                            allEntries.AddRange(catalog.Mods);
                            if (!isPrivate)
                                publicEntries.AddRange(catalog.Mods);
                            GameLogger.Log("CATALOG", $"Fetched {catalog.Mods.Count} entr(y/ies) from '{url}'");
                        }

                        if (catalog?.Collections != null)
                        {
                            foreach (var collection in catalog.Collections)
                                CatalogCollectionMerger.TagSource(collection, url);
                            allCollections.AddRange(catalog.Collections);
                            if (!isPrivate)
                                publicCollections.AddRange(catalog.Collections);
                            GameLogger.Log("CATALOG", $"Fetched {catalog.Collections.Count} collection(s) from '{url}'");
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

                if (successfulSources > 0)
                {
                    // Save before merging: a merge can add private URLs to an entry
                    // also present in a public catalog. Private data stays in memory.
                    SaveCache(CatalogMerger.MergeAndDeduplicate(publicEntries),
                        CatalogCollectionMerger.MergeAndDeduplicate(publicCollections));
                    _cachedEntries = CatalogMerger.MergeAndDeduplicate(allEntries);
                    _cachedCollections = CatalogCollectionMerger.MergeAndDeduplicate(allCollections);
                    GameLogger.Log("CATALOG",
                        $"Catalog refresh complete: {_cachedEntries.Count} total mod(s), {_cachedCollections.Count} collection(s) merged");
                    notifyRefreshed = true;
                    if (errors.Count > 0)
                        refreshErrors = string.Join("\n", errors);
                }
                else if (errors.Count > 0)
                {
                    // A failed private refresh cannot keep old permissions or URLs.
                    if (ModConfig.Instance.CatalogTokenFiles.Count > 0)
                    {
                        _cachedEntries = new List<CatalogExportEntry>();
                        _cachedCollections = new List<ModCollection>();
                        notifyRefreshed = true;
                    }
                    GameLogger.Log("CATALOG", "Catalog refresh failed: all URLs returned errors", LogLevel.Error);
                    refreshErrors = string.Join("\n", errors);
                }
                else
                {
                    GameLogger.Log("CATALOG", "Catalog refresh complete: no new entries");
                    notifyRefreshed = true;
                }
            }
            finally
            {
                _isFetching = false;
                // External refreshers (the sources dialog and downloader) do
                // not own the manager's banner. Notify after clearing the flag
                // so every subscriber renders the completed state immediately.
                if (notifyRefreshed)
                    OnRefreshed?.Invoke();
                if (refreshErrors != null)
                    OnRefreshFailed?.Invoke(refreshErrors);
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
            _cachedCollections = null;
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


        private static void SaveCache(List<CatalogExportEntry> entries, List<ModCollection> collections)
        {
            try
            {
                var path = GetCachePath();
                var root = new CatalogCacheRoot { Mods = entries, Collections = collections };
                var json = JsonSerializer.Serialize(root, JsonOptionsProvider.WriteIndentedOptions);
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
