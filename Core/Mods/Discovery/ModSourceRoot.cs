namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Describes a single source of mods — a root directory, remote catalog
    /// URL, or any other entry point that a <see cref="IModScanner"/> can
    /// enumerate. Each root carries a <see cref="Priority"/> used by
    /// <see cref="ModRegistry"/> for deduplication: when two mods with the
    /// same ID are discovered under different roots, the mod from the
    /// lower-priority root takes precedence.
    /// </summary>
    public class ModSourceRoot
    {
        /// <summary>Filesystem path or URL to the mod source root.</summary>
        public string Url { get; set; }

        /// <summary>
        /// Deduplication precedence. Lower value = higher priority.
        /// <c>0</c> is the user mods directory, <c>1</c> is the built-in
        /// game mods directory, higher values are for remote/external sources.
        /// </summary>
        public int Priority { get; set; }
    }
}
