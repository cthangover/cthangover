using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Cthangover.Core.Events;
using Cthangover.Core.Mods.Discovery;
using Godot;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Mod discovery and index. Scans <c>user://mods/</c> (falling back to
    /// <c>res://mods/</c>) on first <c>Initialize</c>, creates the
    /// appropriate <c>IModFileProvider</c> per entry (folder vs zip), reads
    /// <c>manifest.json</c> from each, and stores the result.
    ///
    /// <b>Deduplication:</b> when two mods share the same
    /// <see cref="ModManifest.Id"/> (or fall back to the same filesystem
    /// name), the one with the higher <see cref="ModManifest.Version"/>
    /// is kept and the lower one is skipped with a warning. Mods without
    /// a version are treated as <c>"0.0.0.0"</c>.
    ///
    /// After discovery, delegates to <c>ModAssemblyLoader</c> for
    /// precompiled DLLs and then to <c>ModCompiler</c> for source-based
    /// mods — the ordering matters because source mods may depend on APIs
    /// defined in precompiled DLLs. <c>Reload</c> clears state and
    /// re-runs discovery; <c>ModManager</c> hooks this to also flush its
    /// own caches.
    /// </summary>
    public class ModRegistry : IModRegistry
    {
        private static readonly Lazy<ModRegistry> instance = new(() => new ModRegistry());
        /// <summary>Thread-safe singleton.</summary>
        public static ModRegistry Instance => instance.Value;

        private readonly Dictionary<string, IModInfo> mods = new();
        private readonly Dictionary<string, IModInfo> allDiscoveredMods = new();
        private readonly Dictionary<string, int> _existingPriorities = new();
        private List<string> orderedModIds;
        private bool initialized;
        private bool pathsPrepared;

        /// <summary>
        /// Serialises the discovery/compile/finalize pipeline. The synchronous
        /// <see cref="Initialize"/> holds it for the whole pipeline so a second
        /// caller blocks and then observes <see cref="initialized"/> instead of
        /// running a competing load; the launcher-boot path takes it per phase.
        /// <see cref="pipelineRunning"/> lets a re-entrant call from inside the
        /// pipeline (e.g. <see cref="GetOrderedModIds"/> during finalize) return
        /// without recursing.
        /// </summary>
        private readonly object pipelineLock = new();
        private bool pipelineRunning;

        /// <summary>
        /// How long a background caller waits for the launcher-driven load
        /// before giving up. The launcher finalizes on the main thread, so a
        /// background thread may safely block here; 2 minutes covers large
        /// modpacks with cold Roslyn compilation.
        /// </summary>
        private const int LauncherReadyTimeoutMs = 120000;

        /// <summary>
        /// Roots resolved on the main thread during <see cref="PreparePaths"/>.
        /// Discovery itself is pure file I/O, but scanners resolve their roots via
        /// <c>ProjectSettings.GlobalizePath</c>, which is Godot-API and must stay
        /// on the main thread — so roots are captured here once, then consumed by
        /// <see cref="DiscoverAndCompile"/> on the background thread.
        /// </summary>
        private readonly List<(IModScanner Scanner, string Url, int Priority)> resolvedRoots = new();

        /// <summary>Precompiled DLL assemblies collected during <see cref="DiscoverAndCompile"/> and registered by <see cref="FinalizeGodotInit"/>.</summary>
        private List<(string ModId, Assembly Assembly)> _precompiledAssemblies = new();

        /// <summary>C# source assemblies compiled during <see cref="DiscoverAndCompile"/> and registered by <see cref="FinalizeGodotInit"/>.</summary>
        private List<(string ModId, Assembly Assembly, List<(string FilePath, string Source)> Sources)> _compiledAssemblies = new();

        private ModRegistry()
        {
            Mods = mods;
        }

        public static string ModsPath => ResolveModsRoots().FirstOrDefault().Path;

        public static List<(string Path, int Priority)> AllModsRoots => ResolveModsRoots();

        /// <summary>True once discovery has run at least once.</summary>
        public bool IsInitialized => Volatile.Read(ref initialized);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IModInfo> Mods { get; }

        /// <summary>
        /// Snapshot of all discovered mods before disabled-filtering,
        /// intended for the mods configuration UI to display the full
        /// list regardless of enable state.
        /// </summary>
        public IReadOnlyDictionary<string, IModInfo> AllDiscoveredMods => allDiscoveredMods;

        /// <summary>
        /// Idempotent bootstrap: scans the mods directory, creates
        /// providers, reads manifests, resolves duplicate IDs by
        /// version, loads precompiled DLLs, then compiles source-based
        /// mods. Returns immediately on subsequent calls.
        ///
        /// This is the synchronous single-threaded entry point used by tests,
        /// tooling, and non-launcher scene launches. The launcher boot path
        /// instead calls <see cref="PreparePaths"/>, <see cref="DiscoverAndCompile"/>
        /// (on a background thread) and <see cref="FinalizeGodotInit"/> in order,
        /// so the heavy compile phase never blocks the main thread's rendering.
        /// </summary>
        public void Initialize()
        {
            if (Volatile.Read(ref initialized))
                return;

            // When booting through the launcher scene, the launcher drives the
            // pipeline itself (background compile + main-thread finalize) so the
            // main thread can keep rendering. A main-thread caller must return
            // rather than block — the main thread is the one that will finalize.
            // A background caller waits for the launcher instead of silently
            // observing an empty registry, which is what used to make lazy
            // factories cache "no mods" during the boot window.
            if (BootController.IsLauncherBoot && !BootController.ModsInitialized)
            {
                if (GameEnvironment.IsMainThread)
                    return;

                if (!BootController.WaitForModsInitialized(LauncherReadyTimeoutMs))
                    GameLogger.Log("MODS_REGISTRY", "Timed out waiting for launcher mod initialization", LogLevel.Error);
                return;
            }

            lock (pipelineLock)
            {
                if (initialized || pipelineRunning)
                    return;

                pipelineRunning = true;
                try
                {
                    PreparePaths();
                    DiscoverAndCompile();
                    FinalizeGodotInit();
                }
                finally
                {
                    pipelineRunning = false;
                }
            }
        }

        /// <summary>
        /// Resolves everything that requires Godot API on the main thread and is
        /// needed by the background compile phase:
        /// <list type="bullet">
        /// <item>mod discovery registry (scanner/loader reflection discovery)</item>
        /// <item>scanner roots via <c>ProjectSettings.GlobalizePath</c></item>
        /// <item>compiler/native-loader cache roots and Mono build output dir</item>
        /// </list>
        /// Idempotent; safe to call before <see cref="DiscoverAndCompile"/>.
        /// </summary>
        public void PreparePaths()
        {
            lock (pipelineLock)
            {
                if (pathsPrepared)
                    return;
                pathsPrepared = true;

                ModDiscoveryRegistry.Initialize();
                ModCompiler.WarmUpPaths();
                _ = ModNativeLoader.CacheRoot;
                _ = ModConfig.Instance;
                DisabledModsStore.WarmUp();
                ModCollectionStore.WarmUp();

                resolvedRoots.Clear();
                foreach (var scanner in ModDiscoveryRegistry.Scanners)
                {
                    foreach (var root in scanner.GetRoots())
                        resolvedRoots.Add((scanner, root.Url, root.Priority));
                }
            }
        }

        /// <summary>
        /// Godot-free phase: scans the pre-resolved roots, reads manifests,
        /// hashes mods, filters/validates them, loads native libraries,
        /// loads precompiled DLLs and compiles C# source mods — all pure .NET
        /// (System.IO, Roslyn, Assembly.Load) with no Godot API. Runs on a
        /// background thread during launcher boot. Collected assemblies are
        /// stored internally and registered later by <see cref="FinalizeGodotInit"/>.
        /// </summary>
        public void DiscoverAndCompile(CancellationToken cancellationToken = default)
        {
            lock (pipelineLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // The mods dictionary is being rebuilt — any previously cached load
                // order (possibly computed from an empty registry during the boot
                // window) is now stale and must be recomputed.
                orderedModIds = null;

                var anyRootFound = false;

                foreach (var (scanner, url, priority) in resolvedRoots)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!scanner.IsCanProvide(url))
                    {
                        GameLogger.Log("MODS_REGISTRY",
                            $"Scanner '{scanner.Name}' skipped root '{url}': not available",
                            LogLevel.Debug);
                        continue;
                    }

                    anyRootFound = true;
                    GameLogger.Log("MODS_REGISTRY",
                        $"Scanning root '{url}' with '{scanner.Name}' priority={priority}");
                    DiscoverMods(scanner, url, priority, cancellationToken);
                }

                if (!anyRootFound)
                {
                    GameLogger.Log("MODS_REGISTRY", "No mods roots found, skipping mod loading", LogLevel.Error);
                    GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: END (no mods root), {mods.Count} mod(s)");
                    return;
                }

                foreach (var kvp in mods)
                    allDiscoveredMods[kvp.Key] = kvp.Value;

                FilterDisabledMods();
                ValidateDependencies();

                ModNativeLoader.LoadNativeDlls(mods.Values, cancellationToken);
                _precompiledAssemblies = ModAssemblyLoader.LoadPrecompiledDlls(mods.Values, cancellationToken);
                _compiledAssemblies = ModCompiler.CompileAll(mods, cancellationToken);
            }
        }

        /// <summary>
        /// Godot-bound phase, must run on the main thread after
        /// <see cref="DiscoverAndCompile"/>: registers compiled/precompiled
        /// assemblies into the game's registries (reflection fan-out that may
        /// instantiate Godot types), loads GDScript mods, and computes the mod
        /// load order. Completes the pipeline started by <see cref="Initialize"/>.
        /// </summary>
        public void FinalizeGodotInit()
        {
            lock (pipelineLock)
            {
                if (initialized)
                    return;

                foreach (var (modId, assembly) in _precompiledAssemblies)
                    ModAssemblyLoader.RegisterAssembly(assembly, modId);

                foreach (var (modId, assembly, sourceFiles) in _compiledAssemblies)
                {
                    ModAssemblyLoader.RegisterAssembly(assembly, modId);
                    ScriptTypeRegistry.RegisterAssembly(assembly, modId, sourceFiles);
                }

                ModGDScriptLoader.LoadGDScripts(mods);

                // Discard any order computed during the launcher boot window:
                // a main-thread caller can reach GetOrderedModIds while the
                // background compile is still filling the registry, and that
                // partial order must not survive into the finalized mod set.
                orderedModIds = null;
                var orderedIds = GetOrderedModIds();
                var orderedList = new List<(string ModId, int Priority)>();
                for (var i = 0; i < orderedIds.Count; i++)
                    orderedList.Add((orderedIds[i], i));
                ScriptTypeRegistry.SetModOrder(orderedList);

                // Publish readiness only after every registry is populated:
                // a concurrent reader must never observe IsInitialized while
                // scripts/GDScripts are still missing. The volatile write is
                // ordered before the wait-handle release in MarkModsInitialized.
                Volatile.Write(ref initialized, true);
                BootController.MarkModsInitialized();

                GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: loaded {mods.Count} mod(s)");
                foreach (var kvp in mods)
                    GameLogger.Log("MODS_REGISTRY", $"  [{kvp.Key}] '{kvp.Value.Name}' v{kvp.Value.Version ?? "?"} provider={kvp.Value.FileProvider.GetType().Name}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Discards all loaded mods and re-runs discovery. The caller
        /// (typically <c>ModManager.Reload</c>) is responsible for
        /// additionally flushing asset caches.
        /// </summary>
        public void Reload()
        {
            lock (pipelineLock)
            {
                foreach (var kvp in mods)
                    ModEventBus.Instance?.ClearForMod(kvp.Key);

                Volatile.Write(ref initialized, false);
                mods.Clear();
                allDiscoveredMods.Clear();
                _existingPriorities.Clear();
                orderedModIds = null;
                _precompiledAssemblies = new List<(string ModId, Assembly Assembly)>();
                _compiledAssemblies = new List<(string ModId, Assembly Assembly, List<(string FilePath, string Source)> Sources)>();
                ScriptTypeRegistry.Clear();
                ModNativeLoader.FreeAll();
                Initialize();
            }
        }

        /// <summary>
        /// Invalidates the cached ordered mod list so the next call to
        /// <c>GetOrderedModIds</c> recomputes it. Called by the UI after
        /// the user changes the manual load order.
        /// </summary>
        public void InvalidateOrder()
        {
            orderedModIds = null;
        }

        /// <summary>
        /// Looks up a mod by ID. Triggers <c>Initialize</c> if not
        /// yet done, so callers don't need a separate guard.
        /// </summary>
        public IModInfo GetMod(string id)
        {
            Initialize();
            mods.TryGetValue(id, out var mod);
            return mod;
        }

		/// <summary>
		/// Returns mod IDs ordered by <c>depends</c> (topological sort),
		/// then by user-defined manual order, with alphabetical fallback
		/// for determinism. Dependencies always precede their dependents —
		/// manual ordering only applies between unrelated mods.
		/// When <paramref name="filterDisabled"/> is <c>true</c> (default),
		/// the result is cached and persisted, but only once the registry
		/// has been finalized: during the launcher boot window a main-thread
		/// caller would otherwise freeze an order computed from the
		/// half-built mod set for the rest of the session — and overwrite
		/// the saved manual order with an empty list. When
		/// <paramref name="filterDisabled"/> is <c>false</c>, the order is
		/// computed from <see cref="AllDiscoveredMods"/> every call (no
		/// caching) and is NOT persisted to <see cref="ModLoadOrderStore"/>
		/// — this variant is intended for the mod configuration UI.
		/// </summary>
		public IReadOnlyList<string> GetOrderedModIds(bool filterDisabled = true)
		{
			Initialize();

			if (filterDisabled && orderedModIds != null)
				return orderedModIds;

			var source = filterDisabled ? mods : allDiscoveredMods;

			var dependsMap = new Dictionary<string, IReadOnlyList<string>>();
			foreach (var kvp in source)
			{
				var manifest = ((ModInfo)kvp.Value).Manifest;
				dependsMap[kvp.Key] = manifest?.DependencyIds ?? new List<string>();
			}

			var manualOrder = ModLoadOrderStore.Sync(source.Keys.ToList());
			var result = ModLoadOrder.Compute(dependsMap, manualOrder);

			if (filterDisabled && IsInitialized)
			{
				orderedModIds = result;
				ModLoadOrderStore.Save(orderedModIds);
			}

			GameLogger.Log("MODS_REGISTRY", $"GetOrderedModIds(filterDisabled={filterDisabled}): {result.Count} mod(s) ordered");
			for (int i = 0; i < result.Count; i++)
				GameLogger.Log("MODS_REGISTRY", $"  [{i}] {result[i]}", LogLevel.Debug);

			return result;
		}

        private static List<(string Path, int Priority)> ResolveModsRoots()
        {
            var roots = new List<(string Path, int Priority)>();
            ModDiscoveryRegistry.Initialize();

            foreach (var scanner in ModDiscoveryRegistry.Scanners)
            {
                foreach (var root in scanner.GetRoots())
                {
                    if (scanner.IsCanProvide(root.Url))
                        roots.Add((root.Url, root.Priority));
                }
            }

            return roots;
        }

        /// <summary>
        /// Holds a discovered mod candidate before deduplication.
        /// </summary>
        private struct ModCandidate
        {
            public string Id;
            public ModInfo Info;
            public string FilesystemEntryName;
            public IModFileProvider Provider;
            public ModManifest Manifest;
            public int RootPriority;
        }

        private void DiscoverMods(IModScanner scanner, string modsRoot, int rootPriority, CancellationToken cancellationToken)
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

            RegisterCandidates(candidates);
        }

        /// <summary>
        /// Translates a localization key only on the Godot main thread. The
        /// discovery/validation phase may run on a background thread during boot
        /// (before any locale is loaded anyway), so <c>TranslationServer.Translate</c>
        /// must not be called there — the raw key is returned instead.
        /// </summary>
        private static string Tr(string key)
        {
            if (!GameEnvironment.IsMainThread)
                return key;
            return TranslationServer.Translate(key);
        }

        private void FilterDisabledMods()
        {
            var disabled = DisabledModsStore.Load();
            if (disabled.Count == 0)
                return;

            var toRemove = new HashSet<string>();
            foreach (var id in mods.Keys)
            {
                if (disabled.Contains(id))
                {
                    toRemove.Add(id);
                    var info = (ModInfo)mods[id];
                    info.LoadFailureReasons.Add(string.Format(Tr("mods/disabled_reason"), id));
                }
            }

            bool changed;
            do
            {
                changed = false;
                foreach (var kvp in mods)
                {
                    if (toRemove.Contains(kvp.Key))
                        continue;

                    var manifest = ((ModInfo)kvp.Value).Manifest;
                    var depIds = manifest?.DependencyIds;
                    if (depIds == null || depIds.Count == 0)
                        continue;

                    foreach (var depId in depIds)
                    {
                        if (toRemove.Contains(depId))
                        {
                            toRemove.Add(kvp.Key);
                            var info = (ModInfo)kvp.Value;
                            info.LoadFailureReasons.Add(string.Format(Tr("mods/dep_disabled_reason"), depId));
                            GameLogger.Log("MODS_REGISTRY",
                                $"Mod '{kvp.Key}' disabled because dependency '{depId}' is disabled",
                                LogLevel.Warning);
                            changed = true;
                            break;
                        }
                    }
                }
            } while (changed);

            foreach (var id in toRemove)
            {
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' filtered out (disabled)", LogLevel.Message);
            }
        }

        private void ValidateDependencies()
        {
            var toRemove = new HashSet<string>();

            foreach (var kvp in mods)
            {
                var modId = kvp.Key;
                var manifest = ((ModInfo)kvp.Value).Manifest;
                if (manifest?.Depends == null)
                    continue;

                foreach (var dep in manifest.Depends)
                {
                    if (!dep.IsValid)
                    {
                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        info.LoadFailureReasons.Add(string.Format(Tr("mods/invalid_dep_format"), dep.Error));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' has invalid dependency: {dep.Error}",
                            LogLevel.Error);
                        break;
                    }

                    if (!mods.ContainsKey(dep.ModId))
                    {
                        if (dep.IsOptional)
                            continue;

                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        info.LoadFailureReasons.Add(string.Format(Tr("mods/required_mod_not_loaded"), dep.ModId));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' requires '{dep.ModId}' but it is not loaded",
                            LogLevel.Error);
                        break;
                    }

                    if (!dep.IsSatisfiedBy(mods[dep.ModId].Version ?? "0.0.0.0"))
                    {
                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        var actualVersion = mods[dep.ModId].Version ?? "0.0.0.0";
                        info.LoadFailureReasons.Add(
                            string.Format(Tr("mods/version_mismatch"),
                                $"{dep.ModId}{dep.Operator}{dep.VersionRaw}", actualVersion));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' requires {dep.ModId}{dep.Operator}{dep.VersionRaw} but loaded version is {actualVersion}",
                            LogLevel.Error);
                        break;
                    }
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var id in toRemove)
            {
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' filtered out (dependency validation failed)", LogLevel.Message);
            }
        }

        private void RegisterCandidates(List<ModCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (mods.TryGetValue(candidate.Id, out var existing))
                {
                    var existingInfo = (ModInfo)existing;

                    if (candidate.RootPriority < _existingPriorities.GetValueOrDefault(candidate.Id, int.MaxValue))
                    {
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{candidate.Id}' ({candidate.FilesystemEntryName}) v{candidate.Info.Version} " +
                            $"replaces v{existingInfo.Version} from '{existingInfo.Id}' (higher-priority source)",
                            LogLevel.Warning);

                        existingInfo.FileProvider.Dispose();
                        mods[candidate.Id] = candidate.Info;
                        _existingPriorities[candidate.Id] = candidate.RootPriority;
                    }
                    else if (candidate.RootPriority == _existingPriorities.GetValueOrDefault(candidate.Id, int.MaxValue))
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
                    _existingPriorities[candidate.Id] = candidate.RootPriority;
                }
            }
        }

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
