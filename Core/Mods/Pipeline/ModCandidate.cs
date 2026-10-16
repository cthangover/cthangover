namespace Cthangover.Core.Mods.Pipeline
{
    using Cthangover.Core.Mods.Discovery;

    /// <summary>
    /// Holds a discovered mod candidate before deduplication: one filesystem
    /// entry of a scanner root, already materialised into an
    /// <see cref="IModFileProvider"/> with a parsed manifest, but not yet
    /// decided against same-id candidates from other roots (the decision —
    /// root priority first, then version — happens in
    /// <c>ModDiscoveryService.RegisterCandidates</c>).
    ///
    /// A struct on purpose: candidates are short-lived loop-local values and
    /// only the winning candidate's <see cref="Info"/> (with its provider)
    /// survives into the registry; the losers' providers are disposed during
    /// deduplication so zips and folder handles never leak.
    /// </summary>
    public struct ModCandidate
    {
        /// <summary>Manifest id, or the loader's filesystem-derived fallback id when the manifest omits it.</summary>
        public string Id;

        /// <summary>Runtime model built from the manifest; owns the provider the registry will keep on win.</summary>
        public ModInfo Info;

        /// <summary>Raw filesystem entry name (folder or file name) as reported by the scanner, for log messages.</summary>
        public string FilesystemEntryName;

        /// <summary>Provider handle used to read the manifest; disposed if this candidate loses deduplication.</summary>
        public IModFileProvider Provider;

        /// <summary>Parsed manifest.json; <c>null</c> candidates never reach deduplication.</summary>
        public ModManifest Manifest;

        /// <summary>Dedup priority of the root this candidate was found in (lower wins, mirrors scanner priorities).</summary>
        public int RootPriority;
    }
}
