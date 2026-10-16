namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Mods.Discovery;

    /// <summary>
    /// Static helpers for resolving the mods roots (the scanned sources of
    /// mods, e.g. <c>user://mods/</c> and the game-dir <c>mods/</c>). Extracted
    /// from <see cref="ModRegistry"/> so tools and test setups can query roots
    /// without touching the registry's singleton state: these lookups only
    /// initialise the discovery registry and probe the filesystem — they never
    /// trigger mod loading or the boot pipeline.
    ///
    /// Threading contract: <c>GetRoots</c>/<c>IsCanProvide</c> may globalise
    /// paths through <c>ProjectSettings</c>, so call these members from the
    /// Godot main thread (callers — mod manager UI, uninstaller, test setups —
    /// already do). The boot pipeline resolves the same roots once per launch
    /// in <c>ModBootPipeline.PreparePaths</c> and never uses this class.
    ///
    /// <see cref="ModRegistry.ModsPath"/> and
    /// <see cref="ModRegistry.AllModsRoots"/> remain as delegating static
    /// properties for source compatibility with existing callers.
    /// </summary>
    public static class ModRoots
    {
        /// <summary>Primary mods root (the first accessible scanner root, lowest priority number).</summary>
        public static string ModsPath => ResolveModsRoots().FirstOrDefault().Path;

        /// <summary>All currently accessible mods roots with their dedup priorities; used by the uninstaller's writable-root check.</summary>
        public static List<(string Path, int Priority)> AllModsRoots => ResolveModsRoots();

        /// <summary>
        /// Re-resolves every registered scanner's roots on each call (no
        /// caching — callers may run after new scanners were registered by
        /// freshly loaded mod assemblies, which must be visible immediately).
        /// A root is listed only when its scanner still answers
        /// <see cref="IModDiscoveryProvider.IsCanProvide"/>, so roots whose
        /// backing directory disappeared drop out of the result.
        /// </summary>
        public static List<(string Path, int Priority)> ResolveModsRoots()
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
    }
}
