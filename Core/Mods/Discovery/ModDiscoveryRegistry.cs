using System;
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
    ///
    /// <b>Thread safety:</b> the provider lists are static and mutated by
    /// registration/unregistration/reset (the test suite resets them between
    /// classes) while parallel threads look up providers — the boot pipeline
    /// on a background thread, the mod-manager UI, test collections. Every
    /// mutation and every lookup goes through <see cref="gate"/>; public list
    /// access returns a snapshot so callers iterating
    /// <see cref="Scanners"/>/<see cref="Loaders"/> never race a concurrent
    /// reset (a wiped list mid-iteration used to surface as a rare
    /// NullReferenceException in <c>FindLoader</c> and
    /// <c>ModRoots.ResolveModsRoots</c>).
    /// </summary>
    public static class ModDiscoveryRegistry
    {
        private static readonly object gate = new();
        private static readonly List<IModScanner> _scanners = new();
        private static readonly List<IModLoader> _loaders = new();
        private static bool _initialized;

        /// <summary>
        /// All registered scanners in registration order. When iterating
        /// for matching, walk the list in reverse (LIFO) so that mod-added
        /// scanners can override core scanners. Snapshot: safe to iterate
        /// while another thread registers or resets.
        /// </summary>
        public static IReadOnlyList<IModScanner> Scanners
        {
            get { lock (gate) return _scanners.ToArray(); }
        }

        /// <summary>
        /// All registered loaders in registration order. LIFO lookup:
        /// the last registered loader that answers <c>true</c> to
        /// <see cref="IModDiscoveryProvider.IsCanProvide"/> wins. Snapshot:
        /// safe to iterate while another thread registers or resets.
        /// </summary>
        public static IReadOnlyList<IModLoader> Loaders
        {
            get { lock (gate) return _loaders.ToArray(); }
        }

        /// <summary>
        /// Idempotent initialisation. Scans all currently loaded assemblies
        /// for <c>IModScanner</c> and <c>IModLoader</c> implementations
        /// and registers them. Called once by <see cref="ModRegistry"/>.
        /// </summary>
        public static void Initialize()
        {
            // The scan runs under the lock and the flag flips only after the
            // lists are filled: a concurrent caller waits instead of returning
            // early into "initialized but still empty" (no scanner/loader found).
            lock (gate)
            {
                if (_initialized)
                    return;

                _scanners.AddRange(Reflections.FindAndCreate<IModScanner>());
                _loaders.AddRange(Reflections.FindAndCreate<IModLoader>());
                _initialized = true;

                GameLogger.Log("MODS_DISCOVERY",
                    $"Initialized: {_scanners.Count} scanner(s), {_loaders.Count} loader(s)");
            }
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
            var scanners = Reflections.FindAndCreate<IModScanner>(assembly);
            var loaders = Reflections.FindAndCreate<IModLoader>(assembly);

            lock (gate)
            {
                _scanners.AddRange(scanners);
                _loaders.AddRange(loaders);
            }
        }

        /// <summary>
        /// Removes scanners and loaders contributed by the given assembly —
        /// the unload pass of the assembly consumer pipeline. Matching by
        /// concrete type assembly keeps built-in providers untouched.
        /// </summary>
        public static void UnregisterAssembly(Assembly assembly)
        {
            lock (gate)
            {
                _scanners.RemoveAll(s => s.GetType().Assembly == assembly);
                _loaders.RemoveAll(l => l.GetType().Assembly == assembly);
            }
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
            var scanners = Scanners;
            for (int i = scanners.Count - 1; i >= 0; i--)
            {
                if (scanners[i].IsCanProvide(url))
                    return scanners[i];
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
            var loaders = Loaders;
            for (int i = loaders.Count - 1; i >= 0; i--)
            {
                if (loaders[i].IsCanProvide(url))
                    return loaders[i];
            }
            return null;
        }
    }
}
