using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cthangover.Core.Factories;
using Cthangover.Core.Interactive;
using Cthangover.Core.Scenes;
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

        public bool IsInitialized => registry.IsInitialized;

        public IReadOnlyDictionary<string, IModInfo> Mods => registry.Mods;

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

        public void Reload()
        {
            foreach (var resolver in resolvers.Values)
                resolver.ClearCache();
            ModConflictRegistry.Clear();
            registry.Reload();
        }

        /// <summary>
        /// Flushes all resolver caches without re-running mod discovery. Called
        /// after the launcher boot: during the background load the main thread may
        /// have triggered lazy collection with an empty registry, caching empty
        /// results (e.g. <c>FileListResolver</c>, <c>TextureResolver</c>) that must
        /// be discarded once the real mod set is known.
        /// </summary>
        public void ClearCaches()
        {
            foreach (var resolver in resolvers.Values)
                resolver.ClearCache();
            ModConflictRegistry.Clear();
        }

        public void RegisterResolver(IModAssetResolver resolver)
        {
            resolver.Initialize(context);
            resolvers[resolver.Id] = resolver;
        }

        public void RegisterResolversFromAssembly(Assembly assembly)
        {
            foreach (var resolver in Reflections.FindAndCreate<IModAssetResolver>(assembly))
                RegisterResolver(resolver);
        }

        public T GetResolver<T>() where T : class, IModAssetResolver
        {
            return resolvers.Values.OfType<T>().FirstOrDefault();
        }
        
        public IModInfo GetMod(string id)
        {
            return context.GetMod(id);
        }

        public bool FileExists(string modId, string path)
        {
            return context.FileExists(modId, path);
        }

        public IEnumerable<string> ListFiles(string modId, string directory = "")
        {
            return context.ListFiles(modId, directory);
        }

        public string ReadFileText(string modId, string path)
        {
            return context.ReadFileText(modId, path);
        }

        public byte[] ReadFileBinary(string modId, string path)
        {
            return context.ReadFileBinary(modId, path);
        }

        public Stream OpenStream(string modId, string path)
        {
            return context.OpenStream(modId, path);
        }

        public string GetFileSystemPath(string modId, string path)
        {
            return context.GetFileSystemPath(modId, path);
        }

        public string ReadResolvedText(string modId, string path)
        {
            return context.ReadResolvedText(modId, path);
        }

        public string ReadResolvedText(string modId, string path, IModFileProvider providerOverride)
        {
            return context.ReadResolvedText(modId, path, providerOverride);
        }

        public T ReadJson<T>(string modId, string path) where T : class
        {
            return context.ReadJson<T>(modId, path);
        }
    }
}
