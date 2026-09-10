namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Base contract for the strategy pattern that drives mod discovery.
    /// Both <see cref="IModScanner"/> (knows where to find mods) and
    /// <see cref="IModLoader"/> (knows how to open a specific mod entry)
    /// extend this interface so that <see cref="ModDiscoveryRegistry"/>
    /// can discover them via <c>Reflections.FindAndCreate</c> and apply
    /// uniform LIFO lookup semantics — the last-registered provider that
    /// answers <c>true</c> to <see cref="IsCanProvide"/> wins.
    /// </summary>
    public interface IModDiscoveryProvider
    {
        /// <summary>Human-readable name used in log messages (e.g. "UserData", "Zip").</summary>
        string Name { get; }

        /// <summary>
        /// Returns <c>true</c> if this provider can handle the given
        /// filesystem path or URL. For scanners this is an accessibility
        /// guard (does the directory exist? is the server reachable?);
        /// for loaders this is a type-dispatch check (is it a folder? a zip?).
        /// </summary>
        bool IsCanProvide(string url);
    }
}
