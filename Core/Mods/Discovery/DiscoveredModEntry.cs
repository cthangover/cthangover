namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// A single mod entry discovered by an <see cref="IModScanner"/> during
    /// root enumeration. Carries the full <see cref="Url"/> (path or URI)
    /// that a matching <see cref="IModLoader"/> can resolve, plus a short
    /// <see cref="EntryName"/> used for logging and as a fallback mod ID
    /// when <c>manifest.json</c> does not specify one.
    /// </summary>
    public class DiscoveredModEntry
    {
        /// <summary>Full filesystem path or URL to the mod entry.</summary>
        public string Url { get; set; }

        /// <summary>
        /// Short human-readable name — folder name, zip filename, or
        /// last segment of a remote URL. Used in log messages and as a
        /// fallback mod ID.
        /// </summary>
        public string EntryName { get; set; }
    }
}
