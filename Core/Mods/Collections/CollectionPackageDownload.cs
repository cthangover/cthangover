using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cthangover.Core.Mods
{
    /// <summary>Retrieves a selected collection's cover/locale package with the same resume and grant refresh as mods.</summary>
    public static class CollectionPackageDownload
    {
        /// <summary>Downloads and installs the selected release; legacy administrative DTOs without packages remain documents.</summary>
        public static async Task<bool> InstallAsync(ModCollection collection)
        {
            if (collection.Origin != ModCollectionOrigin.Catalog || collection.Downloads == null || collection.Downloads.Count == 0) return true;
            var release = collection.Downloads.FirstOrDefault(d => d.Version == collection.Version);
            if (release?.ArchiveSha256 is not { Length: 64 } || release.SizeBytes > CollectionPackage.MaxArchiveBytes) return false;
            CollectionPackage.ValidateId(collection.Id);
            if (Directory.Exists(Path.Combine(ModCollectionStore.UserPath, collection.Id))) return true;
            var path = Path.Combine(Path.GetTempPath(), "collection-" + release.ArchiveSha256 + ".part");
            using var client = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            if (!await ResumableDownload.RunAsync(client, release, path, () => Renew(collection, release), null)) return false;
            try
            {
                CollectionPackage.Install(path, Path.Combine(ModCollectionStore.UserPath, collection.Id), collection, release);
                return true;
            }
            finally { File.Delete(path); }
        }

        private static async Task<System.Collections.Generic.List<string>> Renew(ModCollection selected, CatalogEntryDownload original)
        {
            while (CatalogService.IsFetching) await Task.Delay(25);
            await CatalogService.RefreshAsync(forceRefresh: true);
            var entry = CatalogService.CachedCollections?.FirstOrDefault(c => c.Id == selected.Id
                && (selected.SourceUrls == null || c.SourceUrls?.Intersect(selected.SourceUrls).Any() == true));
            return entry?.Downloads?.FirstOrDefault(d => d.Version == original.Version && d.ArchiveSha256 == original.ArchiveSha256)?.Urls;
        }
    }
}
