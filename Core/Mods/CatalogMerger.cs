using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Pure merging rules for the online mod catalog. Extracted from
    /// <see cref="CatalogService"/> so the conflict/provenance logic is unit
    /// testable without network or Godot.
    /// <para/>
    /// Normal case: all catalogs publishing the same mod id are combined into one
    /// entry — the newest version wins, downloads of every version are merged and
    /// <see cref="CatalogExportEntry.SourceUrls"/> lists every catalog that
    /// published it. Exceptional case: the same id, version and title appear in
    /// several catalogs with <b>different content hashes</b>; those are kept as
    /// separate entries flagged with <see cref="CatalogExportEntry.VariantConflict"/>
    /// (each carrying its own source URL) so the UI can warn the player about a
    /// likely counterfeit build instead of silently picking one.
    /// </summary>
    public static class CatalogMerger
    {
        /// <summary>
        /// Tags a freshly fetched entry with the catalog URL it came from,
        /// preserving URLs already present.
        /// </summary>
        public static void TagSource(CatalogExportEntry entry, string url)
        {
            if (entry == null || string.IsNullOrWhiteSpace(url) || url == entry.SourceUrls?.FirstOrDefault())
                return;

            entry.SourceUrls ??= new List<string>();
            if (!entry.SourceUrls.Contains(url))
                entry.SourceUrls.Add(url);
        }

        /// <summary>
        /// Merges raw catalog entries (possibly from several catalogs) into the
        /// deduplicated, conflict-aware list consumed by the UI and downloader.
        /// </summary>
        public static List<CatalogExportEntry> MergeAndDeduplicate(List<CatalogExportEntry> entries)
        {
            var result = new List<CatalogExportEntry>();
            if (entries == null)
                return result;

            foreach (var group in entries.GroupBy(e => e.Id))
            {
                var list = group.ToList();
                if (!HasVariantConflict(list))
                {
                    result.Add(MergeGroup(list));
                    continue;
                }

                foreach (var variant in list.GroupBy(GetVariantKey))
                {
                    var merged = MergeGroup(variant.ToList());
                    merged.VariantConflict = true;
                    result.Add(merged);
                }
            }

            return result;
        }

        /// <summary>
        /// Selects the entry to install. When conflicting variants share an id, the
        /// caller passes the source URL of the chosen one; without a source (or
        /// when it matches nothing) the first matching entry wins.
        /// </summary>
        public static CatalogExportEntry FindEntry(
            IReadOnlyList<CatalogExportEntry> entries, string modId, string sourceUrl)
        {
            if (entries == null || string.IsNullOrEmpty(modId))
                return null;

            CatalogExportEntry fallback = null;
            foreach (var entry in entries)
            {
                if (!string.Equals(entry.Id, modId, StringComparison.OrdinalIgnoreCase))
                    continue;

                fallback ??= entry;

                if (string.IsNullOrEmpty(sourceUrl) || entry.SourceUrls == null)
                    continue;

                foreach (var url in entry.SourceUrls)
                {
                    if (string.Equals(url, sourceUrl, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }
            }

            return fallback;
        }

        /// <summary>
        /// True when two entries describe the same mod (id, version, title) but
        /// their content hashes differ — one of the catalogs is serving a modified
        /// build or a different mod under the same name.
        /// </summary>
        private static bool HasVariantConflict(List<CatalogExportEntry> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (!string.Equals(a.Version, b.Version, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(a.Name ?? a.Id, b.Name ?? b.Id, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(GetVariantKey(a), GetVariantKey(b), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Content identity of an entry: its own version plus the hash of that
        /// version's download. Entries with the same key are the same build and may
        /// be merged across catalogs.
        /// </summary>
        private static string GetVariantKey(CatalogExportEntry entry)
            => $"{entry?.Version ?? "?"}|{GetVersionHash(entry, entry?.Version)}";

        private static string GetVersionHash(CatalogExportEntry entry, string version)
        {
            if (entry?.Downloads == null || entry.Downloads.Count == 0)
                return "nohash";

            CatalogEntryDownload match = null;
            foreach (var download in entry.Downloads)
            {
                if (string.Equals(download.Version, version, StringComparison.OrdinalIgnoreCase))
                {
                    match = download;
                    break;
                }
            }

            match ??= entry.Downloads[0];
            if (!string.IsNullOrEmpty(match.InstallSha256))
                return match.InstallSha256;
            if (!string.IsNullOrEmpty(match.ArchiveSha256))
                return match.ArchiveSha256;
            return "nohash";
        }

        private static CatalogExportEntry MergeGroup(List<CatalogExportEntry> group)
        {
            var best = group.OrderByDescending(e => e.Version ?? "0.0.0",
                Comparer<string>.Create((a, b) => VersionHelper.Compare(a, b)))
                .First();

            var allDownloads = group.SelectMany(e => e.Downloads ?? new List<CatalogEntryDownload>());
            best.Downloads = MergeDownloadVersions(allDownloads);
            best.SourceUrls = MergeSources(group);

            if (group.Any(e => e.VariantConflict == true))
                best.VariantConflict = true;

            return best;
        }

        private static List<string> MergeSources(List<CatalogExportEntry> group)
        {
            var sources = new List<string>();
            foreach (var entry in group)
            {
                if (entry.SourceUrls == null)
                    continue;

                foreach (var url in entry.SourceUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url) && !sources.Contains(url))
                        sources.Add(url);
                }
            }

            return sources.Count > 0 ? sources : null;
        }

        private static List<CatalogEntryDownload> MergeDownloadVersions(
            IEnumerable<CatalogEntryDownload> downloads)
        {
            var merged = new Dictionary<string, CatalogEntryDownload>();

            foreach (var dl in downloads)
            {
                if (string.IsNullOrEmpty(dl.Version))
                    continue;

                if (merged.TryGetValue(dl.Version, out var existing))
                {
                    var urlSet = new HashSet<string>(existing.Urls ?? new List<string>());
                    if (dl.Urls != null)
                        foreach (var u in dl.Urls)
                            if (!string.IsNullOrWhiteSpace(u))
                                urlSet.Add(u);
                    existing.Urls = urlSet.ToList();

                    if (dl.SizeBytes > 0 && existing.SizeBytes == 0)
                        existing.SizeBytes = dl.SizeBytes;

                    if (!string.IsNullOrEmpty(dl.ArchiveSha256) && string.IsNullOrEmpty(existing.ArchiveSha256))
                        existing.ArchiveSha256 = dl.ArchiveSha256;

                    if (!string.IsNullOrEmpty(dl.InstallSha256) && string.IsNullOrEmpty(existing.InstallSha256))
                        existing.InstallSha256 = dl.InstallSha256;
                }
                else
                {
                    merged[dl.Version] = new CatalogEntryDownload
                    {
                        Version = dl.Version,
                        Urls = dl.Urls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList()
                            ?? new List<string>(),
                        SizeBytes = dl.SizeBytes,
                        ArchiveSha256 = dl.ArchiveSha256,
                        InstallSha256 = dl.InstallSha256,
                    };
                }
            }

            return merged.Values.OrderByDescending(d => d.Version ?? "0.0.0",
                Comparer<string>.Create((a, b) => VersionHelper.Compare(a, b))).ToList();
        }
    }
}
