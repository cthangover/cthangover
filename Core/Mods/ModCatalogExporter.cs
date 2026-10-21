using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Generates catalog JSON entries from installed mods. Two entry
    /// points:
    ///
    /// <see cref="ExportFromFolder"/> — for folder mods under
    /// development. Creates a .zip archive next to the mod folder
    /// (if one doesn't already exist), computes both SHA256 checksums,
    /// and writes a companion .json catalog entry file.
    ///
    /// <see cref="ExportFromZip"/> — for already-packaged zip mods.
    /// Computes checksums from the existing archive and writes the
    /// companion .json file.
    ///
    /// The output .json file is named after the archive
    /// (<c>mod_name.json</c>) and placed alongside it. This file is a
    /// ready-to-embed snippet for the <c>"mods"</c> array in the
    /// online catalog.
    /// </summary>
    public static class ModCatalogExporter
    {
        /// <summary>
        /// Exports a catalog entry for a folder mod. If no zip archive
        /// for this mod exists in <paramref name="outputDir"/>, one is
        /// created from the mod's content files (<see cref="ModContentFiles"/>:
        /// the folder's <c>.git/</c> and other hidden entries stay out of the
        /// published archive). The companion .json file is then written
        /// alongside the zip.
        /// </summary>
        /// <param name="modInfo">Loaded mod info with manifest metadata.</param>
        /// <param name="entry">Populated catalog entry DTO (download URL,
        /// category, tags, etc. must be set by the caller).</param>
        /// <param name="outputDir">Directory where the zip and json will be placed
        /// (typically the mods root directory).</param>
        /// <returns>Absolute path to the written .json file.</returns>
        public static string ExportFromFolder(ModInfo modInfo, CatalogExportEntry entry, string outputDir)
        {
            var folderPath = modInfo.FileProvider.RootPath;
            var zipName = entry.Id + ".zip";
            var zipPath = Path.Combine(outputDir, zipName);

            if (!File.Exists(zipPath))
            {
                Directory.CreateDirectory(outputDir);
                File.Delete(zipPath + ".tmp");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in ModContentFiles.Enumerate(folderPath))
                        archive.CreateEntryFromFile(file, ModPath.Normalize(Path.GetRelativePath(folderPath, file)), CompressionLevel.Optimal);
                }
                GameLogger.Log("MODS_EXPORT", $"Created archive '{zipPath}' from folder mod '{entry.Id}'");
            }

            var downloadEntry = EnsureDownloadEntry(entry);
            downloadEntry.ArchiveSha256 = ModHasher.ComputeArchiveSha256(zipPath);
            downloadEntry.InstallSha256 = ModHasher.ComputeInstallSha256(modInfo.FileProvider,
                ModConfig.Instance.CatalogIgnorePatterns);
            downloadEntry.SizeBytes = new FileInfo(zipPath).Length;

            var jsonPath = Path.ChangeExtension(zipPath, ".json");
            WriteJsonFile(jsonPath, entry);

            GameLogger.Log("MODS_EXPORT", $"Exported catalog entry for '{entry.Id}' → '{jsonPath}'");
            return jsonPath;
        }

        /// <summary>
        /// Exports a catalog entry for a zip mod. The archive already
        /// exists; only the companion .json file is written.
        /// </summary>
        /// <param name="modInfo">Loaded mod info with manifest metadata.</param>
        /// <param name="entry">Populated catalog entry DTO.</param>
        /// <param name="outputDir">Directory where the json will be placed
        /// (typically the mods root directory).</param>
        /// <returns>Absolute path to the written .json file.</returns>
        public static string ExportFromZip(ModInfo modInfo, CatalogExportEntry entry, string outputDir)
        {
            var zipPath = modInfo.FileProvider.RootPath;

            var downloadEntry = EnsureDownloadEntry(entry);
            downloadEntry.ArchiveSha256 = ModHasher.ComputeArchiveSha256(zipPath);
            downloadEntry.InstallSha256 = ModHasher.ComputeInstallSha256(modInfo.FileProvider,
                ModConfig.Instance.CatalogIgnorePatterns);
            downloadEntry.SizeBytes = new FileInfo(zipPath).Length;

            var jsonPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(zipPath) + ".json");
            WriteJsonFile(jsonPath, entry);

            GameLogger.Log("MODS_EXPORT", $"Exported catalog entry for '{entry.Id}' → '{jsonPath}'");
            return jsonPath;
        }

        private static CatalogEntryDownload EnsureDownloadEntry(CatalogExportEntry entry)
        {
            entry.Downloads ??= new System.Collections.Generic.List<CatalogEntryDownload>();
            var downloadEntry = entry.Downloads.Count > 0 ? entry.Downloads[0] : null;
            if (downloadEntry == null || !string.Equals(downloadEntry.Version, entry.Version, System.StringComparison.OrdinalIgnoreCase))
            {
                downloadEntry = new CatalogEntryDownload { Version = entry.Version };
                entry.Downloads.Add(downloadEntry);
            }
            return downloadEntry;
        }

        private static void WriteJsonFile(string path, CatalogExportEntry entry)
        {
            var json = JsonSerializer.Serialize(entry, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
            File.WriteAllText(path, json);
        }
    }
}
