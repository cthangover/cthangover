using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Mods.ModManager
{
    /// <summary>
    /// One row of the mod manager grid: everything the tile and the detail page
    /// need to render a mod, regardless of whether it exists locally, in the
    /// online catalog, or both. The manager builds these in
    /// <see cref="ModManagerModel.Reload"/> by merging
    /// <c>ModRegistry.AllDiscoveredMods</c> with <c>CatalogService.CachedEntries</c>.
    /// <para/>
    /// <see cref="Status"/> is derived (not stored by the registry) from the local
    /// version, the selected catalog download and the local install hash — call
    /// <see cref="RecomputeStatus"/> after changing <see cref="SelectedDownload"/>,
    /// <see cref="IsDisabled"/> or the install state.
    /// </summary>
    public class ModCardData
    {
        /// <summary>Stable mod id shared by the local mod and the catalog entry.</summary>
        public string Id { get; set; }

        /// <summary>Localized display title (local manifest name or catalog locale name).</summary>
        public string Title { get; set; }

        /// <summary>Author string from whichever source is available.</summary>
        public string Author { get; set; }

        /// <summary>Localized description.</summary>
        public string Description { get; set; }

        /// <summary>Installed version string, or null for catalog-only mods.</summary>
        public string LocalVersion { get; set; }

        /// <summary>Manifest category (local) or catalog category (remote), flags combined.</summary>
        public CategoryType Category { get; set; } = CategoryType.Other;

        /// <summary>Loaded local mod, or null when only the catalog knows this id.</summary>
        public ModInfo LocalInfo { get; set; }

        /// <summary>Merged catalog entry, or null for local-only (development) mods.</summary>
        public CatalogExportEntry Catalog { get; set; }

        /// <summary>True when a local copy exists (including an install done in this session).</summary>
        public bool IsInstalled { get; set; }

        /// <summary>True when the local copy is listed in <c>disabled_mods.json</c>.</summary>
        public bool IsDisabled { get; set; }

        /// <summary>True when <see cref="ModUninstaller"/> may delete the local copy.</summary>
        public bool IsDeletable { get; set; }

        /// <summary>True for the mod manager itself, which cannot be disabled or deleted.</summary>
        public bool IsProtected { get; set; }

        /// <summary>Derived tile state; see <see cref="RecomputeStatus"/>.</summary>
        public ModItemStatus Status { get; set; }

        /// <summary>Catalog downloads, newest first (as merged by the catalog service).</summary>
        public List<CatalogEntryDownload> Downloads { get; set; } = new();

        /// <summary>Version chosen in the tile/detail combo box; defaults to the newest download.</summary>
        public CatalogEntryDownload SelectedDownload { get; set; }

        /// <summary>True when an online catalog entry exists for this mod.</summary>
        public bool HasRemote => Catalog != null;

        /// <summary>
        /// True when several catalogs publish this id with the same version and
        /// title but different content — the tile and detail page warn about it.
        /// </summary>
        public bool IsConflict => Catalog?.VariantConflict == true;

        /// <summary>
        /// How many <see cref="ModCollectionKind.Game"/> collections reference
        /// this mod (directly or through includes). Computed by the manager
        /// screen via <see cref="ModCollectionGallery.FindGamesUsingMod"/> on
        /// every grid refresh; zero means the mod belongs to no playable game.
        /// </summary>
        public int CollectionsCount { get; set; }

        /// <summary>Catalog URLs that published this variant, or null for local-only mods.</summary>
        public IReadOnlyList<string> SourceUrls => Catalog?.SourceUrls;

        /// <summary>First catalog URL; used to install exactly this variant and to match cards after reload.</summary>
        public string PrimarySource => ModCatalogSource.FirstUrl(SourceUrls);

        /// <summary>
        /// Identity of a card across model reloads: the mod id plus the catalog
        /// variant it represents. Needed because conflicting variants share an id.
        /// </summary>
        public string MatchKey => $"{Id}|{PrimarySource ?? string.Empty}";

        /// <summary>True when the tile should show the enable/disable switch.</summary>
        public bool CanToggleEnabled => IsInstalled && !IsProtected;

        /// <summary>Version to display next to the title: installed version or newest catalog version.</summary>
        public string DisplayVersion => !string.IsNullOrEmpty(LocalVersion)
            ? LocalVersion
            : SelectedDownload?.Version ?? Catalog?.Version;

        /// <summary>
        /// Derives <see cref="Status"/> from the local and selected remote versions.
        /// Priority: not installed → disabled → version comparison →
        /// hash comparison. A missing catalog entry always yields
        /// <see cref="ModItemStatus.Installed"/> for a local copy.
        /// </summary>
        public void RecomputeStatus()
        {
            if (!IsInstalled)
            {
                Status = ModItemStatus.NotInstalled;
                return;
            }

            if (IsDisabled)
            {
                Status = ModItemStatus.Disabled;
                return;
            }

            var remoteVersion = SelectedDownload?.Version ?? Catalog?.Version;
            if (string.IsNullOrEmpty(remoteVersion) || string.IsNullOrEmpty(LocalVersion))
            {
                Status = ModItemStatus.Installed;
                return;
            }

            var comparison = VersionHelper.Compare(remoteVersion, LocalVersion);
            if (comparison > 0)
            {
                Status = ModItemStatus.UpdateAvailable;
                return;
            }

            if (comparison < 0)
            {
                Status = ModItemStatus.DowngradeAvailable;
                return;
            }

            var remoteHash = SelectedDownload?.InstallSha256;
            if (!string.IsNullOrEmpty(remoteHash)
                && !string.IsNullOrEmpty(LocalInfo?.InstallSha256)
                && !string.Equals(LocalInfo.InstallSha256, remoteHash, System.StringComparison.OrdinalIgnoreCase))
            {
                Status = ModItemStatus.ModifiedLocally;
                return;
            }

            Status = ModItemStatus.Installed;
        }
    }
}
