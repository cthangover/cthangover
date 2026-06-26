using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Public read-only view of the mod registry. Exposes only the mod
    /// list — mutation and discovery logic is encapsulated in
    /// <c>ModRegistry</c> — so that external consumers (factories, UI)
    /// can enumerate loaded mods without being able to alter the registry
    /// state.
    /// </summary>
    public interface IModRegistry
    {
        /// <summary>
        /// Read-only snapshot of currently loaded mods, keyed by mod ID.
        /// </summary>
        IReadOnlyDictionary<string, IModInfo> Mods { get; }
    }

}