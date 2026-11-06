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
    /// Ids are compared case-insensitively, like the downloader and the mods
    /// folder do. When several catalog listings share an id, every content hash
    /// they publish must agree: the hash of each listing's current version and
    /// the hash of every version present in more than one listing. A missing
    /// hash cannot be checked and counts as a mismatch.
    /// <para/>
    /// Agreeing listings are combined into one entry — the newest version wins,
    /// downloads of every version are merged and
    /// <see cref="CatalogExportEntry.SourceUrls"/> lists every catalog. On any
    /// mismatch nothing is merged: each listing stays a separate entry flagged
    /// with <see cref="CatalogExportEntry.VariantConflict"/>. The client cannot
    /// tell which catalog serves the genuine build (a higher version from a
    /// foreign catalog could replace a private mod), so all of them are shown as
    /// unsafe and the catalog administrators sort it out.
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

            foreach (var group in entries.Where(e => e?.Id != null).GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                var list = group.ToList();
                if (list.Count == 1 || !HasHashMismatch(list))
                {
                    result.Add(MergeGroup(list));
                    continue;
                }

                foreach (var listing in list)
                {
                    var separate = MergeGroup(new List<CatalogExportEntry> { listing });
                    separate.VariantConflict = true;
                    result.Add(separate);
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
        /// True when listings of one id disagree on any content hash: their
        /// current versions differ in hash, a shared version has two hashes, or
        /// a hash is missing and cannot be compared.
        /// </summary>
        private static bool HasHashMismatch(List<CatalogExportEntry> list)
        {
            string current = null;
            var byVersion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in list)
            {
                var hash = GetVersionHash(entry, entry.Version);
                if (hash == null)
                    return true;
                if (current != null && !string.Equals(current, hash, StringComparison.OrdinalIgnoreCase))
                    return true;
                current = hash;

                foreach (var download in entry.Downloads ?? new List<CatalogEntryDownload>())
                {
                    var downloadHash = GetDownloadHash(download);
                    if (downloadHash == null || string.IsNullOrEmpty(download.Version))
                        return true;
                    if (byVersion.TryGetValue(download.Version, out var known)
                        && !string.Equals(known, downloadHash, StringComparison.OrdinalIgnoreCase))
                        return true;
                    byVersion[download.Version] = downloadHash;
                }
            }

            return false;
        }

        /// <summary>Hash of the entry's own version download (first download as fallback), or null without one.</summary>
        private static string GetVersionHash(CatalogExportEntry entry, string version)
        {
            if (entry?.Downloads == null || entry.Downloads.Count == 0)
                return null;

            CatalogEntryDownload match = null;
            foreach (var download in entry.Downloads)
            {
                if (string.Equals(download.Version, version, StringComparison.OrdinalIgnoreCase))
                {
                    match = download;
                    break;
                }
            }

            return GetDownloadHash(match ?? entry.Downloads[0]);
        }

        /// <summary>Install hash (content identity) when published, otherwise the archive hash; null without either.</summary>
        private static string GetDownloadHash(CatalogEntryDownload download)
        {
            if (!string.IsNullOrEmpty(download?.InstallSha256))
                return download.InstallSha256;
            if (!string.IsNullOrEmpty(download?.ArchiveSha256))
                return download.ArchiveSha256;
            return null;
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
