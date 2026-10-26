using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Action plan that turns a resolved <see cref="ModCollectionGraph"/> into
    /// concrete steps for the collections screen: which mods to download, which
    /// disabled mods to re-enable, and what blocks a full apply. The split keeps
    /// the decision logic testable — actual file writes and downloads stay in
    /// <c>ModDownloader</c> / <c>DisabledModsStore</c> / the UI.
    /// <para/>
    /// Enable list contains mods that are already on disk but disabled; mods in
    /// <see cref="Installed"/> state are left untouched. <see cref="Missing"/>
    /// and <see cref="Mismatches"/> are informational: the UI shows them as
    /// warnings and blocks applying while something is missing, because enabling
    /// a mod whose dependency cannot be loaded would only make the registry drop
    /// it at next boot.
    /// </summary>
    public class ModCollectionPlan
    {
        /// <summary>Mods to download and install, in graph order (foundations first).</summary>
        public List<ModCollectionInstallItem> Install { get; } = new();

        /// <summary>Ids of installed but disabled mods to remove from <c>disabled_mods.json</c>.</summary>
        public List<string> Enable { get; } = new();

        /// <summary>Ids that are neither installed nor available in any catalog.</summary>
        public List<string> Missing { get; } = new();

        /// <summary>Ids whose installed version does not satisfy the collection's pin, or that have no satisfying download.</summary>
        public List<string> Mismatches { get; } = new();

        /// <summary>True when applying the collection would not change anything.</summary>
        public bool IsEmpty => Install.Count == 0 && Enable.Count == 0;

        /// <summary>True when the collection cannot be assembled completely.</summary>
        public bool HasProblems => Missing.Count > 0 || Mismatches.Count > 0;

        /// <summary>
        /// Builds the plan for a resolved graph using the same merged catalog the
        /// downloader will read. Pass the catalog list the graph was resolved
        /// with so node states and chosen downloads stay consistent.
        /// </summary>
        public static ModCollectionPlan Build(
            ModCollectionGraph graph,
            IReadOnlyList<CatalogExportEntry> catalogEntries)
        {
            var plan = new ModCollectionPlan();
            if (graph == null)
                return plan;

            var catalogById = IndexCatalog(catalogEntries);

            foreach (var node in graph.Nodes)
            {
                switch (node.State)
                {
                    case ModCollectionNodeState.InCatalog:
                        AddInstall(plan, node, catalogById);
                        break;

                    case ModCollectionNodeState.Disabled:
                        plan.Enable.Add(node.ModId);
                        break;

                    case ModCollectionNodeState.Missing:
                        plan.Missing.Add(node.ModId);
                        break;

                    case ModCollectionNodeState.VersionMismatch:
                        plan.Mismatches.Add(node.ModId);
                        break;
                }
            }

            return plan;
        }

        private static void AddInstall(
            ModCollectionPlan plan,
            ModCollectionNode node,
            Dictionary<string, CatalogExportEntry> catalogById)
        {
            if (!catalogById.TryGetValue(node.ModId, out var entry))
            {
                plan.Mismatches.Add(node.ModId);
                return;
            }

            var download = SelectDownload(entry, node);
            if (download == null)
            {
                plan.Mismatches.Add(node.ModId);
                return;
            }

            plan.Install.Add(new ModCollectionInstallItem
            {
                ModId = node.ModId,
                Version = download.Version,
                SourceUrl = FirstSource(entry),
            });
        }

        /// <summary>
        /// Picks the newest download that satisfies the collection's version pin.
        /// Downloads are ordered newest-first by <see cref="CatalogMerger"/>, so
        /// the first satisfying one is the best choice; without a pin the newest
        /// download is used as-is.
        /// </summary>
        private static CatalogEntryDownload SelectDownload(CatalogExportEntry entry, ModCollectionNode node)
        {
            if (entry?.Downloads == null || entry.Downloads.Count == 0)
                return null;

            foreach (var download in entry.Downloads)
            {
                if (!string.IsNullOrEmpty(download.Version)
                    && node.AcceptsVersion(download.Version))
                {
                    return download;
                }
            }

            return null;
        }

        private static string FirstSource(CatalogExportEntry entry)
        {
            if (entry.SourceUrls == null)
                return null;

            foreach (var url in entry.SourceUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }

            return null;
        }

        private static Dictionary<string, CatalogExportEntry> IndexCatalog(
            IReadOnlyList<CatalogExportEntry> catalogEntries)
        {
            var index = new Dictionary<string, CatalogExportEntry>(System.StringComparer.OrdinalIgnoreCase);
            if (catalogEntries == null)
                return index;

            foreach (var entry in catalogEntries)
            {
                if (!string.IsNullOrEmpty(entry?.Id) && !index.ContainsKey(entry.Id))
                    index[entry.Id] = entry;
            }

            return index;
        }
    }
}
