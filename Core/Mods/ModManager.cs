using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories;
using Cthangover.Core.Interactive;
using Cthangover.Core.Localization;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.Mods.Patches;
using Cthangover.Core.Mods.Pipeline;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Central hub for all mod resource access. Every factory, every
    /// scene loader, every asset resolver ultimately goes through
    /// <c>ModManager</c> to find and read files. It wraps
    /// <c>ModRegistry</c> and delegates domain-specific resolution
    /// to specialised <c>IModAssetResolver</c> implementations.
    ///
    /// Resolution is delegated to a registry of pluggable resolvers —
    /// each asset type (shaders, textures, JSON entities, scenes,
    /// scenarios, interactives, wrappers, patches) has its own resolver
    /// with independent caching. New resolvers can be registered at
    /// runtime by mods via <c>RegisterResolver</c>.
    ///
    /// On <c>Reload</c>, every resolver's cache is flushed so that
    /// hot-reloaded mods are picked up without restarting the game.
    /// </summary>
    public class ModManager
    {
        private static readonly Lazy<ModManager> instance = new(() => new ModManager());

        /// <summary>Thread-safe singleton; construction discovers built-in resolvers via reflection.</summary>
        public static ModManager Instance => instance.Value;

        private readonly ModRegistry registry;
        private readonly ModResolutionContext context;
        private readonly Dictionary<string, IModAssetResolver> resolvers = new();

        private ModManager()
        {
            registry = ModRegistry.Instance;
            context = new ModResolutionContext(registry);

            foreach (var resolver in Reflections.FindAndCreate<IModAssetResolver>())
                RegisterResolver(resolver);

            context.SetResolvers(resolvers);
        }

        /// <summary>True once mod discovery has completed at least once.</summary>
        public bool IsInitialized => registry.IsInitialized;

        /// <summary>Read-only snapshot of loaded (enabled) mods keyed by mod id.</summary>
        public IReadOnlyDictionary<string, IModInfo> Mods => registry.Mods;

        /// <summary>Idempotent bootstrap; delegates to <see cref="ModRegistry.Initialize"/>.</summary>
        public void Initialize()
        {
            registry.Initialize();
        }

        /// <summary>
        /// Launcher-boot phase 1 (main thread): resolves all Godot-dependent paths
        /// (mod roots, cache dirs, config) so the subsequent background phase never
        /// touches Godot API. See <see cref="ModRegistry.PreparePaths"/>.
        /// </summary>
        public void PreparePaths()
        {
            registry.PreparePaths();
        }

        /// <summary>
        /// Launcher-boot phase 2 (background thread): discovery, hashing, native
        /// DLL loading, precompiled DLL loading and Roslyn compilation — pure .NET,
        /// no Godot API. See <see cref="ModRegistry.DiscoverAndCompile"/>.
        /// Cancelling <paramref name="cancellationToken"/> aborts the phase
        /// between mods and during source parsing; Roslyn's final emit cannot be
        /// interrupted, so cancellation is cooperative rather than immediate.
        /// </summary>
        public void DiscoverAndCompile(CancellationToken cancellationToken = default)
        {
            registry.DiscoverAndCompile(cancellationToken);
        }

        /// <summary>
        /// Launcher-boot phase 3 (main thread): registers compiled assemblies,
        /// loads GDScript mods and computes load order. See
        /// <see cref="ModRegistry.FinalizeGodotInit"/>.
        /// </summary>
        public void FinalizeGodotInit()
        {
            registry.FinalizeGodotInit();
        }

        /// <summary>True while an in-process reload is being applied or verified; a second reload is refused meanwhile.</summary>
        public bool IsReloading { get; private set; }

        /// <summary>The last applied (or rejected) in-process reload.</summary>
        public ModReloadResult LastReload { get; private set; }

        /// <summary>
        /// Full synchronous reload of the current selection (every mod with code
        /// is released and loaded again) with every piece of derived state
        /// rebuilt; see <see cref="ApplyReload"/>. Kept for tooling and setups
        /// that change mods on disk and need the new set immediately.
        /// </summary>
        public void Reload()
        {
            var tree = GameEnvironment.IsRuntime ? Engine.GetMainLoop() as SceneTree : null;
            var prep = registry.Reloader.Prepare(Games.GameLaunchArgs.Current, new[] { "*" });
            var refused = IsReloading;
            ApplyPrepared(prep, tree, requireSuccess: false);

            // Nobody awaits VerifyReloadAsync on this synchronous path, so the flag it
            // would clear stayed set and every later reload was refused.
            if (!refused)
                IsReloading = false;
        }

        /// <summary>
        /// First half of an in-process reload, on a background thread: the mod
        /// set of <paramref name="selection"/> discovered and compiled, nothing
        /// running touched (<see cref="ModSetReloader.Prepare"/>).
        /// </summary>
        public System.Threading.Tasks.Task<ModReloadPreparation> PrepareReloadAsync(Games.GameLaunchArgs selection, IReadOnlyCollection<string> force = null, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.Run(() => registry.Reloader.Prepare(selection, force, cancellationToken), cancellationToken);

        /// <summary>
        /// Compiles the mod set of <paramref name="selection"/> into the assembly
        /// cache without loading anything (<see cref="Pipeline.ModSetReloader.WarmUp"/>);
        /// the menu runs it in the background for every game, so picking a game
        /// does not wait for compilation. Blocking, call it off the main thread.
        /// </summary>
        public void WarmUp(Games.GameLaunchArgs selection, CancellationToken cancellationToken = default)
            => registry.Reloader.WarmUp(selection, cancellationToken);

        /// <summary>
        /// Second half, on the main thread: switches to the prepared set and
        /// rebuilds everything derived from it in the boot-phase order (resolver
        /// caches, the <c>IGameRegistry</c> set, initializers, mod settings,
        /// locale, the logical scene table, the session runtime). A failed
        /// preparation is discarded and reported without touching the running
        /// set. Callers leave the current scene first: nodes of released mods
        /// are freed by the unload.
        /// </summary>
        public ModReloadResult ApplyReload(ModReloadPreparation prep, SceneTree tree)
            => ApplyPrepared(prep, tree, requireSuccess: true);

        /// <summary>
        /// Waits (engine frames) until the contexts released by
        /// <paramref name="result"/> are collected, logs survivors as errors,
        /// then publishes <see cref="ModSetReloadedEvent"/>.
        /// </summary>
        public async System.Threading.Tasks.Task<ModReloadResult> VerifyReloadAsync(ModReloadResult result, SceneTree tree)
        {
            try
            {
                if (result.Applied && tree != null)
                {
                    result.Leaked = await result.Ticket.CollectAsync(tree);
                    foreach (var leaked in result.Leaked)
                        GameLogger.Log("MODS_RELOAD", $"Mod '{leaked}' was released but its code is still referenced (see MODS_UNLOAD above) — it stays in memory", LogLevel.Error);
                }
                Events.ModEventBus.Publish(new Events.ModSetReloadedEvent
                {
                    Applied = result.Applied,
                    Loaded = result.Plan?.Load.ToArray() ?? Array.Empty<string>(),
                    Unloaded = result.Plan?.Unload.ToArray() ?? Array.Empty<string>(),
                    Leaked = result.Leaked.ToArray(),
                    Errors = result.Errors.ToArray(),
                });
                return result;
            }
            finally
            {
                IsReloading = false;
            }
        }

        private ModReloadResult ApplyPrepared(ModReloadPreparation prep, SceneTree tree, bool requireSuccess)
        {
            if (IsReloading)
            {
                registry.Reloader.Discard(prep);
                GameLogger.Log("MODS_RELOAD", "Reload refused: another reload is in progress", LogLevel.Warning);
                return LastReload = new ModReloadResult(false, null, null, new[] { "another reload is in progress" });
            }

            if (requireSuccess && !prep.Succeeded)
            {
                registry.Reloader.Discard(prep);
                return LastReload = new ModReloadResult(false, null, null, prep.Errors);
            }

            IsReloading = true;
            foreach (var resolver in resolvers.Values)
                resolver.ClearCache();
            AppServices.Current.ClearRegistries();
            UiHost.Instance?.ClearAll();

            var result = registry.Reloader.Apply(prep, tree);

            // Rebind: same relative order as the boot phases after the mod set
            // is final (Registries → ResourcesReady → ModSettings → Locale →
            // Scenes → runtime), with the exception that UiHost panels stay
            // cleared until the next shell entry.
            foreach (var resolver in resolvers.Values)
                resolver.ClearCache();
            AppServices.Current.RebuildRegistries();
            ModInitializerRegistry.NotifyResourcesReady();
            ModSettingsRegistry.LoadAll();
            LocaleLoader.LoadCurrentLanguage();
            Scenes.SceneManager.RescanScenes();
            Settings.GameData.Instance?.ResetRuntime("mod set reloaded");
            GameLogger.Log("MODS", "Reload complete: registries, initializers, settings, locale, scenes and runtime rebuilt");

            if (tree == null)
                IsReloading = false;
            return LastReload = result;
        }

        /// <summary>Initializes the resolver with the shared context and stores it under its <see cref="IModAssetResolver.Id"/>.</summary>
        /// <summary>Initializes the resolver with the shared context and stores it under its <see cref="IModAssetResolver.Id"/>.</summary>
        public void RegisterResolver(IModAssetResolver resolver)
        {
            resolver.Initialize(context);
            resolvers[resolver.Id] = resolver;
        }

        /// <summary>Discovers <see cref="IModAssetResolver"/> implementations in a mod assembly and registers them.</summary>
        public void RegisterResolversFromAssembly(Assembly assembly)
        {
            foreach (var resolver in Reflections.FindAndCreate<IModAssetResolver>(assembly))
                RegisterResolver(resolver);
        }

        /// <summary>
        /// Removes resolvers whose concrete type comes from the given assembly —
        /// the unload pass of the assembly consumer pipeline. Also flushes the
        /// resolver caches so no cached asset keeps references into unloading
        /// mod types.
        /// </summary>
        public void UnregisterResolversFromAssembly(Assembly assembly)
        {
            foreach (var id in resolvers.Where(kvp => kvp.Value.GetType().Assembly == assembly).Select(kvp => kvp.Key).ToList())
            {
                try
                {
                    resolvers[id].ClearCache();
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS", $"UnregisterResolversFromAssembly: ClearCache for '{id}' failed: {ex.Message}", LogLevel.Warning);
                }
                resolvers.Remove(id);
            }
        }

        public T GetResolver<T>() where T : class, IModAssetResolver
        {
            return resolvers.Values.OfType<T>().FirstOrDefault();
        }
        
        /// <summary>Looks up a loaded mod by id; triggers initialization if needed.</summary>
        public IModInfo GetMod(string id)
        {
            return context.GetMod(id);
        }

        /// <summary>Checks a mod-relative path inside the given mod's provider.</summary>
        public bool FileExists(string modId, string path)
        {
            return context.FileExists(modId, path);
        }

        /// <summary>Shallow listing of a mod directory; entries ending with a slash are directories.</summary>
        public IEnumerable<string> ListFiles(string modId, string directory = "")
        {
            return context.ListFiles(modId, directory);
        }

        /// <summary>Reads a UTF-8 text file from the mod, or null when missing.</summary>
        public string ReadFileText(string modId, string path)
        {
            return context.ReadFileText(modId, path);
        }

        /// <summary>Reads a binary file from the mod, or null when missing.</summary>
        public byte[] ReadFileBinary(string modId, string path)
        {
            return context.ReadFileBinary(modId, path);
        }

        /// <summary>Opens a read-only stream to a mod file; caller disposes. Null when missing.</summary>
        public Stream OpenStream(string modId, string path)
        {
            return context.OpenStream(modId, path);
        }

        /// <summary>Absolute filesystem path for folder mods; null for zip mods and missing files.</summary>
        public string GetFileSystemPath(string modId, string path)
        {
            return context.GetFileSystemPath(modId, path);
        }

        /// <summary>Reads a text file with every include directive recursively expanded.</summary>
        public string ReadResolvedText(string modId, string path)
        {
            return context.ReadResolvedText(modId, path);
        }

        /// <summary>Same as <see cref="ReadResolvedText(string, string)"/> but reads through an explicit provider.</summary>
        public string ReadResolvedText(string modId, string path, IModFileProvider providerOverride)
        {
            return context.ReadResolvedText(modId, path, providerOverride);
        }

        /// <summary>Reads a JSON file (with include expansion) and deserializes it; null on any error.</summary>
        public T ReadJson<T>(string modId, string path) where T : class
        {
            return context.ReadJson<T>(modId, path);
        }
    }
}
