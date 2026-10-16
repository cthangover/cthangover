namespace Cthangover.Core.Mods.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using Cthangover.Core.Mods.Discovery;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Discovery mechanics for one scanner root: enumerates entries, finds a
    /// matching <see cref="IModLoader"/>, materialises a
    /// <see cref="IModFileProvider"/>, reads and parses <c>manifest.json</c>,
    /// computes the install hash, then deduplicates same-id mods across roots
    /// (root priority first, version second — a mod shipped in
    /// <c>user://mods/</c> overrides its game-dir namesake).
    ///
    /// Deliberately pure .NET (file I/O through the provider, Roslyn-free, no
    /// Godot API) so it runs on the background thread during launcher boot;
    /// the roots themselves must have been resolved on the main thread
    /// beforehand (<c>ModBootPipeline.PreparePaths</c>).
    ///
    /// The target dictionaries are owned by <see cref="ModRegistry"/> and
    /// passed in per call: the service is stateless, so a reload simply clears
    /// the registry's dictionaries without any extra reset plumbing here.
    /// </summary>
    public static class ModDiscoveryService
    {
        /// <summary>
        /// Scans a single root already resolved by
        /// <c>ModBootPipeline.PreparePaths</c> and registers every viable
        /// candidate into <paramref name="mods"/>. Individual unreadable
        /// entries are logged and skipped; only a failing scanner enumeration
        /// aborts the whole root (with an error log), keeping one broken entry
        /// from hiding all other mods.
        /// </summary>
        /// <param name="mods">Registry dictionary to fill (enabled mod set).</param>
        /// <param name="existingPriorities">Root priority per already-registered mod id; drives the priority half of deduplication.</param>
        /// <param name="scanner">Scanner that owns the root.</param>
        /// <param name="modsRoot">Root URL to enumerate.</param>
        /// <param name="rootPriority">Dedup priority of the root.</param>
        /// <param name="cancellationToken">Cooperative cancellation between entries.</param>
        public static void DiscoverMods(Dictionary<string, IModInfo> mods, Dictionary<string, int> existingPriorities, IModScanner scanner, string modsRoot, int rootPriority, CancellationToken cancellationToken)
        {
            var candidates = new List<ModCandidate>();

            try
            {
                foreach (var entry in scanner.Discover(modsRoot))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.EntryName == ".gdignore" || entry.EntryName == ".gitkeep")
                        continue;

                    var loader = ModDiscoveryRegistry.FindLoader(entry.Url);
                    if (loader == null)
                        continue;

                    var candidate = TryLoadModFromLoader(loader, entry.Url, entry.EntryName);
                    if (candidate.HasValue)
                    {
                        var c = candidate.Value;
                        c.RootPriority = rootPriority;
                        candidates.Add(c);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to scan directory '{modsRoot}': {ex.Message}", LogLevel.Error);
            }

            RegisterCandidates(mods, existingPriorities, candidates);
        }

        /// <summary>
        /// Deduplicates candidates against what is already registered. Two
        /// axes, checked in this order: a strictly higher root priority always
        /// wins (regardless of version); equal priority falls back to version
        /// comparison — only a strictly newer version replaces. Every losing
        /// candidate's provider (and, on replacement, the displaced mod's
        /// provider) is disposed, so no zip handle or directory watcher leaks.
        /// First candidate seen for an id simply registers and records its
        /// root priority for later comparisons.
        /// </summary>
        private static void RegisterCandidates(Dictionary<string, IModInfo> mods, Dictionary<string, int> existingPriorities, List<ModCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (mods.TryGetValue(candidate.Id, out var existing))
                {
                    var existingInfo = (ModInfo)existing;

                    if (candidate.RootPriority < existingPriorities.GetValueOrDefault(candidate.Id, int.MaxValue))
                    {
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{candidate.Id}' ({candidate.FilesystemEntryName}) v{candidate.Info.Version} " +
                            $"replaces v{existingInfo.Version} from '{existingInfo.Id}' (higher-priority source)",
                            LogLevel.Warning);

                        existingInfo.FileProvider.Dispose();
                        mods[candidate.Id] = candidate.Info;
                        existingPriorities[candidate.Id] = candidate.RootPriority;
                    }
                    else if (candidate.RootPriority == existingPriorities.GetValueOrDefault(candidate.Id, int.MaxValue))
                    {
                        var cmp = VersionHelper.Compare(
                            candidate.Info.Version ?? "0.0.0.0",
                            existingInfo.Version ?? "0.0.0.0");

                        if (cmp > 0)
                        {
                            GameLogger.Log("MODS_REGISTRY",
                                $"Mod '{candidate.Id}' ({candidate.FilesystemEntryName}) v{candidate.Info.Version} " +
                                $"replaces older v{existingInfo.Version} from '{existingInfo.Id}'",
                                LogLevel.Warning);

                            existingInfo.FileProvider.Dispose();
                            mods[candidate.Id] = candidate.Info;
                        }
                        else
                        {
                            var reason = cmp == 0
                                ? $"same version {candidate.Info.Version}"
                                : $"older version {candidate.Info.Version} (already have {existingInfo.Version})";

                            GameLogger.Log("MODS_REGISTRY",
                                $"Skipping mod '{candidate.Id}' ({candidate.FilesystemEntryName}): {reason}",
                                LogLevel.Warning);

                            candidate.Provider.Dispose();
                        }
                    }
                    else
                    {
                        GameLogger.Log("MODS_REGISTRY",
                            $"Skipping mod '{candidate.Id}' ({candidate.FilesystemEntryName}): lower-priority source",
                            LogLevel.Warning);

                        candidate.Provider.Dispose();
                    }
                }
                else
                {
                    mods[candidate.Id] = candidate.Info;
                    existingPriorities[candidate.Id] = candidate.RootPriority;
                }
            }
        }

        /// <summary>
        /// Loads one entry through its loader: creates the provider, reads the
        /// manifest, builds the <see cref="ModInfo"/> and computes the install
        /// SHA256 (a hashing failure only logs — the mod stays loadable).
        /// Returns <c>null</c> for entries without a manifest or on any
        /// loader/manifest exception; the provider is disposed on those paths
        /// so a skipped entry never leaks its handle.
        /// </summary>
        private static ModCandidate? TryLoadModFromLoader(IModLoader loader, string url, string entryName)
        {
            try
            {
                var fileProvider = loader.CreateProvider(url, entryName);
                var manifest = ReadManifest(fileProvider);

                if (manifest == null)
                {
                    GameLogger.Log("MODS_REGISTRY", $"Skipping '{entryName}': no manifest found", LogLevel.Error);
                    fileProvider.Dispose();
                    return null;
                }

                var id = !string.IsNullOrEmpty(manifest.Id) ? manifest.Id : loader.GetFallbackModId(url, entryName);

                var info = new ModInfo
                {
                    Id = id,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Description = manifest.Description,
                    FileProvider = fileProvider,
                    Manifest = manifest,
                };

                try
                {
                    info.InstallSha256 = ModHasher.ComputeInstallSha256(fileProvider,
                        ModConfig.Instance.CatalogIgnorePatterns);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_REGISTRY",
                        $"Failed to compute install SHA256 for mod '{id}': {ex.Message}",
                        LogLevel.Warning);
                }

                return new ModCandidate
                {
                    Id = id,
                    Info = info,
                    FilesystemEntryName = entryName,
                    Provider = fileProvider,
                    Manifest = manifest,
                };
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_REGISTRY",
                    $"Failed to load mod '{entryName}' via {loader.Name}: {ex.Message}",
                    LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Reads and deserialises <c>manifest.json</c> through the provider.
        /// Returns <c>null</c> when the file is absent, empty, or invalid JSON
        /// (parse failure is logged with the provider-agnostic message — the
        /// entry name is not known here), which makes the whole entry skipped
        /// by <see cref="TryLoadModFromLoader"/>.
        /// </summary>
        private static ModManifest ReadManifest(IModFileProvider provider)
        {
            if (!provider.FileExists("manifest.json"))
                return null;

            var json = provider.ReadFileText("manifest.json");
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<ModManifest>(json, JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to parse manifest.json: {ex.Message}", LogLevel.Error);
            }
            return null;
        }
    }
}
