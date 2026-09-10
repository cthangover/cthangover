using System.Collections.Generic;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// A strategy that knows about a source of mods. Each scanner
    /// declares its own root URLs via <see cref="GetRoots"/> — this is
    /// what replaces the old hardcoded <c>ResolveModsRoots()</c>.
    /// <see cref="ModRegistry.Initialize"/> iterates every scanner,
    /// calls <see cref="IModDiscoveryProvider.IsCanProvide"/> on each root
    /// as a guard, and then delegates to <see cref="Discover"/> for actual
    /// enumeration.
    ///
    /// New mod sources (HTTP catalogs, Steam Workshop folders, Git repos)
    /// are added by shipping an <c>IModScanner</c> implementation inside
    /// a mod DLL — <c>Reflections.FindAndCreate</c> picks it up
    /// automatically when the assembly is registered.
    /// </summary>
    public interface IModScanner : IModDiscoveryProvider
    {
        /// <summary>
        /// Returns the root URLs this scanner knows about, together with
        /// their deduplication priorities. Called once per scanner during
        /// <see cref="ModRegistry.Initialize"/>.
        /// </summary>
        IEnumerable<ModSourceRoot> GetRoots();

        /// <summary>
        /// Enumerates mod entries (folders, zip files, remote descriptors)
        /// available at the given root URL. The returned
        /// <see cref="DiscoveredModEntry"/> objects are passed to
        /// <see cref="IModLoader"/> strategies for provider creation.
        /// </summary>
        /// <param name="rootUrl">A root URL previously returned by
        /// <see cref="GetRoots"/>.</param>
        IEnumerable<DiscoveredModEntry> Discover(string rootUrl);
    }
}
