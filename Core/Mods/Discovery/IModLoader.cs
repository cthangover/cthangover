namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// A strategy that can open a specific mod entry and create the
    /// appropriate <see cref="IModFileProvider"/>. After an
    /// <see cref="IModScanner"/> enumerates entries, each entry URL is
    /// tested against all registered loaders — the first one whose
    /// <see cref="IModDiscoveryProvider.IsCanProvide"/> returns <c>true</c>
    /// (checked in LIFO order) creates the provider, reads
    /// <c>manifest.json</c>, and produces a <c>ModCandidate</c>.
    ///
    /// Built-in loaders: <c>FolderModLoader</c> (directories) and
    /// <c>ZipModLoader</c> (.zip archives). Mods can add loaders for
    /// custom formats or remote downloads.
    /// </summary>
    public interface IModLoader : IModDiscoveryProvider
    {
        /// <summary>
        /// Creates an <see cref="IModFileProvider"/> for the given mod
        /// entry. The caller is responsible for disposing it if the mod
        /// is ultimately skipped (deduplication loss, disabled, etc.).
        /// </summary>
        /// <param name="url">Full filesystem path or URL to the mod entry.</param>
        /// <param name="modName">Short name (folder name, zip filename)
        /// used as the provider's <c>Mod</c> identifier.</param>
        IModFileProvider CreateProvider(string url, string modName);

        /// <summary>
        /// Returns the fallback mod ID to use when <c>manifest.json</c>
        /// is present but does not contain an <c>id</c> field. For folder
        /// mods this is the directory name; for zip mods it is the
        /// filename without the <c>.zip</c> extension.
        /// </summary>
        /// <param name="url">Full filesystem path or URL to the mod entry.</param>
        /// <param name="entryName">Short entry name from
        /// <see cref="DiscoveredModEntry.EntryName"/>.</param>
        string GetFallbackModId(string url, string entryName);
    }
}
