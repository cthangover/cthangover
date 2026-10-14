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

        /// <summary>
        /// Hot-reloads the mod set and rebuilds every piece of derived state in
        /// the boot-phase order, so the reloaded mod set is fully usable without
        /// a restart: resolver caches, the IGameRegistry set (data-driven via
        /// <see cref="AppServices.Registries"/>), mod initializers (they
        /// re-register scene routes and gameplay cores), mod settings, locale
        /// and the logical scene table. Adding a registry needs no edit here —
        /// only its line in AppServices.
        /// </summary>
        public void Reload()
        {
            foreach (var resolver in resolvers.Values)
                resolver.ClearCache();
            AppServices.Current.ClearRegistries();
            UiHost.Instance?.ClearAll();
            registry.Reload();

            // Rebind: same relative order as the boot phases after the mod set
            // is final (Registries → ResourcesReady → ModSettings → Locale →
            // Scenes), with the exception that UiHost panels stay cleared until
            // the next shell entry.
            AppServices.Current.RebuildRegistries();
            ModInitializerRegistry.NotifyResourcesReady();
            ModSettingsRegistry.LoadAll();
            LocaleLoader.LoadCurrentLanguage();
            Scenes.SceneManager.RescanScenes();
            GameLogger.Log("MODS", "Reload complete: registries, initializers, settings, locale and scenes rebuilt");
        }

        /// <summary>Initializes the resolver with the shared context and stores it under its <see cref="IModAssetResolver.Id"/>.</summary>
        /// <summary>Initializes the resolver with the shared context and stores it under its <see cref="IModAssetResolver.Id"/>.</summary>
        public void RegisterResolver(IModAssetResolver resolver)
        {
            resolver.Initialize(context);
            resolvers[resolver.Id] = resolver;
        }

        /// <summary>Discovers <see cref="IModAssetResolver"/> implementations in a mod assembly and registers them.</summary>
        /// <summary>Discovers <see cref="IModAssetResolver"/> implementations in a mod assembly and registers them.</summary>
        public void RegisterResolversFromAssembly(Assembly assembly)
        {
            foreach (var resolver in Reflections.FindAndCreate<IModAssetResolver>(assembly))
                RegisterResolver(resolver);
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
