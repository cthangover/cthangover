namespace Cthangover.Core.Mods.Pipeline
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Discovery;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Orchestrates the boot pipeline of the mod subsystem — the three phases
    /// <see cref="PreparePaths"/> → <see cref="DiscoverAndCompile"/> →
    /// <see cref="FinalizeGodotInit"/>, plus the synchronous
    /// <see cref="Initialize"/> that runs all three. Owns the pipeline lock,
    /// the re-entrancy marker, the resolved scanner roots and the staged
    /// assembly lists; <see cref="ModRegistry"/> stays the singleton facade
    /// and delegates its boot-phase methods here.
    ///
    /// Phase split follows the thread contract: <see cref="PreparePaths"/> is
    /// the only phase allowed to touch the Godot API (it runs on the main
    /// thread), <see cref="DiscoverAndCompile"/> is pure .NET for the
    /// background thread, and <see cref="FinalizeGodotInit"/> is Godot-bound
    /// again. The mod dictionaries themselves stay in
    /// <see cref="ModRegistry"/> — they are passed in at construction, so the
    /// facade can keep exposing <c>Mods</c>/<c>AllDiscoveredMods</c> and
    /// reset them on <c>Reload</c> without any extra plumbing.
    /// </summary>
    public class ModBootPipeline
    {
        private readonly ModRegistry registry;
        private readonly Dictionary<string, IModInfo> mods;
        private readonly Dictionary<string, IModInfo> allDiscoveredMods;
        private readonly Dictionary<string, int> existingPriorities;

        /// <summary>
        /// Serialises the discovery/compile/finalize pipeline. The synchronous
        /// <see cref="Initialize"/> holds it for the whole pipeline so a second
        /// caller blocks and then observes the initialized flag instead of
        /// running a competing load; the launcher-boot path takes it per phase.
        /// <see cref="pipelineRunning"/> lets a re-entrant call from inside the
        /// pipeline (e.g. GetOrderedModIds during finalize) return without
        /// recursing. <c>ModRegistry.Reload</c> enters the same lock through
        /// <see cref="PipelineLock"/> — monitors are re-entrant, so the nested
        /// Initialize inside Reload does not deadlock.
        /// </summary>
        private readonly object pipelineLock = new();
        private bool pipelineRunning;

        private bool pathsPrepared;

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

        /// <summary>
        /// Creates the pipeline for one <see cref="ModRegistry"/>. The
        /// dictionaries are shared by reference (never copied): the pipeline
        /// fills them, the facade exposes and clears them.
        /// </summary>
        public ModBootPipeline(ModRegistry registry, Dictionary<string, IModInfo> mods, Dictionary<string, IModInfo> allDiscoveredMods, Dictionary<string, int> existingPriorities)
        {
            this.registry = registry;
            this.mods = mods;
            this.allDiscoveredMods = allDiscoveredMods;
            this.existingPriorities = existingPriorities;
        }

        /// <summary>
        /// The single home of the boot-window policy. During a launcher boot
        /// the pipeline belongs to Boot alone: it drives
        /// <see cref="PreparePaths"/>, <see cref="DiscoverAndCompile"/> (on a
        /// background thread) and <see cref="FinalizeGodotInit"/> in order, so
        /// the heavy compile phase never blocks the main thread's rendering.
        /// While the window is open (<c>BootController.IsLauncherBoot</c>,
        /// before <c>Boot.ModsReady</c>), outside callers must neither trigger
        /// nor consume the half-built mod set — any such call is a boot-order
        /// bug. Re-entrant calls from the pipeline itself are exempt:
        /// <see cref="pipelineRunning"/> marks them (GetOrderedModIds during
        /// FinalizeGodotInit). The window ends at ModsReady: Boot itself
        /// consumes mod data (EnsureRuntime, starter inventory seeding) from
        /// that phase on, so data becomes consumable there. Both
        /// <see cref="Initialize"/> and GetOrderedModIds consult exactly this
        /// property, keeping the refusal semantics identical in both places.
        /// </summary>
        public bool IsLauncherBootWindow => BootController.IsLauncherBoot && !Boot.ModsReady && !pipelineRunning;

        /// <summary>
        /// Idempotent bootstrap: runs the whole pipeline
        /// (<see cref="PreparePaths"/> → <see cref="DiscoverAndCompile"/> →
        /// <see cref="FinalizeGodotInit"/>) unless discovery already ran or is
        /// running. Returns immediately on subsequent calls.
        ///
        /// Synchronous entry point for <c>Boot.RunSync</c>, tests and tooling;
        /// the launcher boot instead drives the phases individually. Any other
        /// caller reaching here inside the boot window is refused with an
        /// error in the log (see <see cref="IsLauncherBootWindow"/>) — lazy
        /// self-initialization outside these paths is deliberately forbidden.
        /// </summary>
        public void Initialize()
        {
            if (registry.InitializedFlag)
                return;

            // Boot-window refusal — the policy lives in IsLauncherBootWindow;
            // this is the only place that logs the refusal, because this is
            // the only entry that would otherwise self-boot the pipeline.
            if (IsLauncherBootWindow)
            {
                GameLogger.Log("MODS_REGISTRY",
                    "Initialize refused: launcher boot owns the mod pipeline " +
                    $"(Boot.Phase={Boot.Phase}). Consume mod data only after Boot.ModsReady, " +
                    "or add a readiness guard like the audio mod's music player does.",
                    LogLevel.Error);
                return;
            }

            lock (pipelineLock)
            {
                if (registry.InitializedFlag || pipelineRunning)
                    return;

                pipelineRunning = true;
                try
                {
                    using (ModAssemblyLoader.BeginBatch())
                    {
                        PreparePaths();
                        DiscoverAndCompile();
                        FinalizeGodotInit();
                    }
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
                CollectionLayoutStore.WarmUp();

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

                // Launcher-driven boot: the batch opens here (the sync path
                // opened it around the whole pipeline) so the background
                // compile routes its assemblies into collectible contexts.
                ModAssemblyLoader.BeginBatch();
                try
                {

                // The mods dictionary is being rebuilt — any previously cached load
                // order (possibly computed from an empty registry during the boot
                // window) is now stale and must be recomputed.
                registry.InvalidateOrder();

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
                    ModDiscoveryService.DiscoverMods(mods, existingPriorities, scanner, url, priority, cancellationToken);
                }

                if (!anyRootFound)
                {
                    GameLogger.Log("MODS_REGISTRY", "No mods roots found, skipping mod loading", LogLevel.Error);
                    GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: END (no mods root), {mods.Count} mod(s)");
                    return;
                }

                foreach (var kvp in mods)
                    allDiscoveredMods[kvp.Key] = kvp.Value;

                ModSetFilter.FilterDisabledMods(mods);
                ModSetFilter.ValidateDependencies(mods);

                ModNativeLoader.LoadNativeDlls(mods.Values, cancellationToken);
                _precompiledAssemblies = ModAssemblyLoader.LoadPrecompiledDlls(mods.Values, cancellationToken);
                _compiledAssemblies = ModCompiler.CompileAll(mods, cancellationToken);
                }
                finally
                {
                    ModAssemblyLoader.EndBatch();
                }
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
                if (registry.InitializedFlag)
                    return;

                // Mark the pipeline as running for the duration of the finalize:
                // GetOrderedModIds below re-enters Initialize, and the
                // launcher-boot refusal guard must exempt the pipeline's own
                // re-entrant calls (pipelineRunning marks them).
                pipelineRunning = true;
                try
                {
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
                    registry.InvalidateOrder();
                    var orderedIds = registry.GetOrderedModIds();
                    var orderedList = new List<(string ModId, int Priority)>();
                    for (var i = 0; i < orderedIds.Count; i++)
                        orderedList.Add((orderedIds[i], i));
                    ScriptTypeRegistry.SetModOrder(orderedList);

                    // Publish readiness only after every registry is populated:
                    // a concurrent reader must never observe IsInitialized while
                    // scripts/GDScripts are still missing. The volatile write is
                    // ordered, so readiness becomes visible to other threads only
                    // once every registry above is fully filled.
                    registry.InitializedFlag = true;

                    GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: loaded {mods.Count} mod(s)");
                    foreach (var kvp in mods)
                        GameLogger.Log("MODS_REGISTRY", $"  [{kvp.Key}] '{kvp.Value.Name}' v{kvp.Value.Version ?? "?"} provider={kvp.Value.FileProvider.GetType().Name}", LogLevel.Debug);
                }
                finally
                {
                    pipelineRunning = false;
                }
            }
        }

        /// <summary>
        /// The pipeline's serialization lock, re-entered by
        /// <c>ModRegistry.Reload</c>: a reload must be mutually exclusive with
        /// the boot phases (monitors are re-entrant, so Reload calling
        /// Initialize under this lock is safe). Not intended for any other
        /// caller — everyone else goes through the phase methods.
        /// </summary>
        internal object PipelineLock => pipelineLock;

        /// <summary>
        /// Discards the assemblies staged by <see cref="DiscoverAndCompile"/>
        /// (the <c>ModRegistry.Reload</c> path): both collections are replaced
        /// with fresh empty lists exactly as in a fresh registry, so a later
        /// finalize cannot double-register stale assemblies.
        /// </summary>
        internal void ResetStagedAssemblies()
        {
            _precompiledAssemblies = new List<(string ModId, Assembly Assembly)>();
            _compiledAssemblies = new List<(string ModId, Assembly Assembly, List<(string FilePath, string Source)> Sources)>();
        }
    }
}
