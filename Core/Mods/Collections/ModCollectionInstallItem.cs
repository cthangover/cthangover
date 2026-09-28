namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One mod that <see cref="ModCollectionPlan"/> wants to download: the id,
    /// the version chosen from the catalog entry (the newest download that
    /// satisfies the collection's version pin, if any) and the catalog URL the
    /// entry came from. The pair <c>(ModId, Version)</c> plus
    /// <see cref="SourceUrl"/> is exactly what
    /// <see cref="ModDownloader.InstallAsync"/> needs.
    /// </summary>
    public class ModCollectionInstallItem
    {
        /// <summary>Canonical mod id to install.</summary>
        public string ModId { get; set; }

        /// <summary>Catalog version to install, or null to let the downloader pick the newest.</summary>
        public string Version { get; set; }

        /// <summary>Catalog URL that published the entry; disambiguates conflicting variants.</summary>
        public string SourceUrl { get; set; }
    }
}
