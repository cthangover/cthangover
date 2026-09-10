using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Central registry that auto-discovers all <see cref="IModScanner"/>
    /// and <see cref="IModLoader"/> implementations via
    /// <c>Reflections.FindAndCreate</c> and provides LIFO-ordered lookup.
    ///
    /// Call <see cref="Initialize"/> once before mod discovery (called by
    /// <see cref="ModRegistry.Initialize"/>). When a mod assembly is loaded,
    /// <see cref="ModAssemblyLoader.RegisterAssembly"/> fans out to
    /// <see cref="RegisterAssembly"/> so that new scanners and loaders
    /// shipped inside the mod are registered — at the end of the list,
    /// meaning they are checked first (LIFO) and can override built-in
    /// providers.
    /// </summary>
    public static class ModDiscoveryRegistry
    {
        private static readonly List<IModScanner> _scanners = new();
        private static readonly List<IModLoader> _loaders = new();
        private static bool _initialized;

        /// <summary>
        /// All registered scanners in registration order. When iterating
        /// for matching, walk the list in reverse (LIFO) so that mod-added
        /// scanners can override core scanners.
        /// </summary>
        public static IReadOnlyList<IModScanner> Scanners => _scanners;

        /// <summary>
        /// All registered loaders in registration order. LIFO lookup:
        /// the last registered loader that answers <c>true</c> to
        /// <see cref="IModDiscoveryProvider.IsCanProvide"/> wins.
        /// </summary>
        public static IReadOnlyList<IModLoader> Loaders => _loaders;

        /// <summary>
        /// Idempotent initialisation. Scans all currently loaded assemblies
        /// for <c>IModScanner</c> and <c>IModLoader</c> implementations
        /// and registers them. Called once by <see cref="ModRegistry"/>.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            _scanners.AddRange(Reflections.FindAndCreate<IModScanner>());
            _loaders.AddRange(Reflections.FindAndCreate<IModLoader>());

            GameLogger.Log("MODS_DISCOVERY",
                $"Initialized: {_scanners.Count} scanner(s), {_loaders.Count} loader(s)");
        }

        /// <summary>
        /// Called by <see cref="ModAssemblyLoader.RegisterAssembly"/> when
        /// a mod DLL is loaded. Discovers new scanner/loader types in the
        /// assembly and appends them to the registered lists. Because
        /// lookup walks the list from the end, these new providers are
        /// checked before built-in ones.
        /// </summary>
        /// <param name="assembly">The assembly to scan for
        /// <see cref="IModScanner"/> and <see cref="IModLoader"/>
        /// implementations.</param>
        public static void RegisterAssembly(Assembly assembly)
        {
            _scanners.AddRange(Reflections.FindAndCreate<IModScanner>(assembly));
            _loaders.AddRange(Reflections.FindAndCreate<IModLoader>(assembly));
        }

        /// <summary>
        /// Returns the first <see cref="IModScanner"/> (checked LIFO —
        /// from the end of the registration list) whose
        /// <see cref="IModDiscoveryProvider.IsCanProvide"/> returns
        /// <c>true</c> for the given root URL, or <c>null</c>.
        /// </summary>
        /// <param name="url">A root URL to test against all registered
        /// scanners.</param>
        public static IModScanner FindScanner(string url)
        {
            for (int i = _scanners.Count - 1; i >= 0; i--)
            {
                if (_scanners[i].IsCanProvide(url))
                    return _scanners[i];
            }
            return null;
        }

        /// <summary>
        /// Returns the first <see cref="IModLoader"/> (checked LIFO) whose
        /// <see cref="IModDiscoveryProvider.IsCanProvide"/> returns
        /// <c>true</c> for the given mod entry URL, or <c>null</c>.
        /// </summary>
        /// <param name="url">A mod entry URL (folder path, zip file, etc.)
        /// to test against all registered loaders.</param>
        public static IModLoader FindLoader(string url)
        {
            for (int i = _loaders.Count - 1; i >= 0; i--)
            {
                if (_loaders[i].IsCanProvide(url))
                    return _loaders[i];
            }
            return null;
        }
    }
}
