namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods.Pipeline;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Mod discovery and index — the singleton facade of the mod subsystem.
    /// Owns the mod dictionaries (<c>mods</c>, <c>AllDiscoveredMods</c>) and
    /// the <c>initialized</c> flag, and delegates the actual work to the
    /// collaborators in <c>Cthangover.Core.Mods.Pipeline</c>: boot-phase
    /// orchestration (<see cref="ModBootPipeline"/>), per-root discovery and
    /// deduplication (<see cref="ModDiscoveryService"/>), disabled/dependency
    /// filtering (<see cref="ModSetFilter"/>) and load-order computation
    /// (<see cref="ModLoadOrderService"/>); static root helpers live in
    /// <see cref="ModRoots"/>.
    ///
    /// <b>Deduplication:</b> when two mods share the same
    /// <see cref="ModManifest.Id"/> (or fall back to the same filesystem
    /// name), the one from the higher-priority root wins, and on equal
    /// priority the higher <see cref="ModManifest.Version"/>; the loser is
    /// skipped with a warning and its file provider is disposed. Mods without
    /// a version are treated as <c>"0.0.0.0"</c>.
    ///
    /// After discovery, the pipeline delegates to <c>ModAssemblyLoader</c>
    /// for precompiled DLLs and then to <c>ModCompiler</c> for source-based
    /// mods — the ordering matters because source mods may depend on APIs
    /// defined in precompiled DLLs. <c>Reload</c> clears state and re-runs
    /// discovery; <c>ModManager</c> hooks this to also flush its own caches.
    /// </summary>
    public class ModRegistry : IModRegistry
    {
        private static readonly Lazy<ModRegistry> instance = new(() => new ModRegistry());
        /// <summary>Thread-safe singleton.</summary>
        public static ModRegistry Instance => instance.Value;

        private readonly Dictionary<string, IModInfo> mods = new();
        private readonly Dictionary<string, IModInfo> allDiscoveredMods = new();
        private readonly Dictionary<string, int> _existingPriorities = new();

        /// <summary>
        /// True once discovery has run at least once. Published by the
        /// pipeline with a volatile write only after every game registry has
        /// been filled (<c>ModBootPipeline.FinalizeGodotInit</c>), so external
        /// readers can trust it as a readiness signal. Field name is load-
        /// bearing: test setups reset this flag via reflection.
        /// </summary>
        private bool initialized;

        private readonly ModBootPipeline pipeline;
        private readonly ModLoadOrderService loadOrder;

        private ModRegistry()
        {
            Mods = mods;
            pipeline = new ModBootPipeline(this, mods, allDiscoveredMods, _existingPriorities);
            loadOrder = new ModLoadOrderService(this, pipeline, mods, allDiscoveredMods);
        }

        /// <summary>Primary mods root (the first accessible scanner root, lowest priority number).</summary>
        public static string ModsPath => ModRoots.ModsPath;

        /// <summary>All currently accessible mods roots with their dedup priorities; used by the uninstaller's writable-root check.</summary>
        public static List<(string Path, int Priority)> AllModsRoots => ModRoots.AllModsRoots;

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
        /// Volatile access to <c>initialized</c> for the boot pipeline: the
        /// flag stays owned by the registry (external readers observe it via
        /// <see cref="IsInitialized"/>), while the pipeline performs the
        /// early-out read in <c>Initialize</c> and the readiness publish in
        /// <c>FinalizeGodotInit</c> at exactly the original points.
        /// </summary>
        internal bool InitializedFlag
        {
            get => Volatile.Read(ref initialized);
            set => Volatile.Write(ref initialized, value);
        }

        /// <summary>
        /// Idempotent bootstrap: scans the mods directory, creates
        /// providers, reads manifests, resolves duplicate IDs by
        /// version, loads precompiled DLLs, then compiles source-based
        /// mods. Returns immediately on subsequent calls. During a launcher
        /// boot the pipeline belongs to Boot alone and other callers are
        /// refused — see <see cref="ModBootPipeline.IsLauncherBootWindow"/>
        /// for the exact policy.
        /// </summary>
        public void Initialize() => pipeline.Initialize();

        /// <summary>
        /// Resolves everything that requires Godot API on the main thread
        /// (scanner roots, cache dirs, config) so the background phase never
        /// touches Godot. Idempotent. See <see cref="ModBootPipeline.PreparePaths"/>.
        /// </summary>
        public void PreparePaths() => pipeline.PreparePaths();

        /// <summary>
        /// Godot-free phase: scans the pre-resolved roots and compiles mods —
        /// pure .NET, safe on a background thread. See
        /// <see cref="ModBootPipeline.DiscoverAndCompile"/>.
        /// </summary>
        public void DiscoverAndCompile(CancellationToken cancellationToken = default)
            => pipeline.DiscoverAndCompile(cancellationToken);

        /// <summary>
        /// Godot-bound phase: registers compiled assemblies, loads GDScript
        /// mods, computes the load order and publishes readiness. See
        /// <see cref="ModBootPipeline.FinalizeGodotInit"/>.
        /// </summary>
        public void FinalizeGodotInit() => pipeline.FinalizeGodotInit();

        /// <summary>
        /// Discards all loaded mods and re-runs discovery. The caller
        /// (typically <c>ModManager.Reload</c>) is responsible for
        /// additionally flushing asset caches. Holds the pipeline lock so a
        /// reload is mutually exclusive with the boot phases; the reset order
        /// mirrors the original initialization inverse — event bus
        /// subscriptions first (while mod ids are still known), then the
        /// registry state, then the static registries.
        /// </summary>
        public void Reload()
        {
            lock (pipeline.PipelineLock)
            {
                foreach (var kvp in mods)
                    ModEventBus.Instance?.ClearForMod(kvp.Key);

                Volatile.Write(ref initialized, false);
                mods.Clear();
                allDiscoveredMods.Clear();
                _existingPriorities.Clear();
                InvalidateOrder();
                pipeline.ResetStagedAssemblies();
                ScriptTypeRegistry.Clear();
                ModNativeLoader.FreeAll();

                // Collectible unload: reverse fan-out through the consumer
                // registry and release each mod's AssemblyLoadContext. A mod
                // whose references survive logs a diagnostic and degrades to
                // the pre-collectible behaviour (assemblies stay until exit);
                // re-initialization still rebuilds a consistent mod set.
                foreach (var loadedId in ModAssemblyLoader.LoadedModIds)
                {
                    var diagnostic = ModAssemblyLoader.UnloadMod(loadedId);
                    if (diagnostic != null)
                        GameLogger.Log("MODS_REGISTRY", $"Reload: mod '{loadedId}' not fully unloaded: {diagnostic}", LogLevel.Warning);
                }

                using (ModAssemblyLoader.BeginBatch())
                    Initialize();
            }
        }

        /// <summary>
        /// Invalidates the cached ordered mod list so the next call to
        /// <see cref="GetOrderedModIds"/> recomputes it. Called by the UI after
        /// the user changes the manual load order.
        /// </summary>
        public void InvalidateOrder() => loadOrder.InvalidateOrder();

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
        /// Returns mod IDs ordered by <c>depends</c>, then by the user's
        /// manual order; semantics, caching and boot-window guards are
        /// documented on <see cref="ModLoadOrderService.GetOrderedModIds"/>.
        /// </summary>
        public IReadOnlyList<string> GetOrderedModIds(bool filterDisabled = true)
            => loadOrder.GetOrderedModIds(filterDisabled);
    }
}
