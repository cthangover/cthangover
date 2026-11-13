using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>Snapshots only a collection document and its presentation files; never embeds referenced mods.</summary>
    public static class CollectionPackage
    {
        public const long MaxArchiveBytes = 16L << 20;

        /// <summary>Creates a deterministic ZIP plus catalog metadata with archive and extracted-content hashes.</summary>
        public static (string Archive, string Json) Export(ModCollection collection, string directory)
        {
            ValidateId(collection.Id);
            var document = JsonSerializer.Deserialize<ModCollection>(JsonSerializer.Serialize(collection), JsonOptionsProvider.ReadOptions);
            document.SourceUrls = null;
            document.Downloads = null;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, document.Id + ".zip");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Write(archive, ModCollectionStore.DocumentName, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptionsProvider.WriteIndentedIgnoreNullOptions)));
                if (!string.IsNullOrEmpty(document.Cover))
                {
                    ValidatePath(document.Cover);
                    Write(archive, document.Cover, collection.Package?.ReadFileBinary(document.Cover)
                        ?? throw new InvalidOperationException("collection cover missing"));
                }
                if (collection.Package != null)
                    CopyLocale(collection.Package, archive, "locale");
            }
            if (new FileInfo(path).Length > MaxArchiveBytes) throw new InvalidOperationException("mod_manager/publish_too_large");
            using var package = new ZipModFileProvider(path, document.Id);
            document.Downloads = new List<CatalogEntryDownload> { new()
            {
                Version = document.Version, ArchiveSha256 = ModHasher.ComputeArchiveSha256(path),
                InstallSha256 = ModHasher.ComputeInstallSha256(package), SizeBytes = new FileInfo(path).Length
            } };
            return (path, JsonSerializer.Serialize(document, JsonOptionsProvider.WriteIndentedIgnoreNullOptions));
        }

        private static void CopyLocale(IModFileProvider provider, ZipArchive archive, string directory)
        {
            foreach (var file in provider.ListFiles(directory).OrderBy(name => name, StringComparer.Ordinal))
            {
                if (file.EndsWith('/')) CopyLocale(provider, archive, file.TrimEnd('/'));
                else { ValidatePath(file); Write(archive, file, provider.ReadFileBinary(file)); }
            }
        }

        private static void Write(ZipArchive archive, string name, byte[] bytes)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var stream = entry.Open();
            stream.Write(bytes);
        }

        /// <summary>Extracts into staging, validates identity and both hashes, then atomically swaps the user package.</summary>
        public static void Install(string archivePath, string target, ModCollection expected, CatalogEntryDownload download)
        {
            ValidateId(expected.Id);
            if (!string.Equals(ModHasher.ComputeArchiveSha256(archivePath), download.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("collection archive hash mismatch");
            var staging = target + ".staging-" + Guid.NewGuid().ToString("N");
            var backup = target + ".backup-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                using var zip = ZipFile.OpenRead(archivePath);
                var entry = zip.GetEntry(ModCollectionStore.DocumentName) ?? throw new InvalidDataException("collection document missing");
                using var reader = new StreamReader(entry.Open());
                var document = JsonSerializer.Deserialize<ModCollection>(reader.ReadToEnd(), JsonOptionsProvider.ReadOptions);
                if (document?.Id != expected.Id || document.Version != download.Version) throw new InvalidDataException("collection identity mismatch");
                long size = 0;
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in zip.Entries)
                {
                    ValidatePath(file.FullName);
                    if (!names.Add(file.FullName)) throw new InvalidDataException("duplicate collection package path");
                    if (file.FullName != ModCollectionStore.DocumentName && file.FullName != document.Cover && !file.FullName.StartsWith("locale/", StringComparison.Ordinal))
                        throw new InvalidDataException("collection package contains unrelated files");
                    size += file.Length;
                    if (size > 64L << 20) throw new InvalidDataException("collection package too large");
                    var path = Path.Combine(staging, file.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    file.ExtractToFile(path);
                }
                using var package = new FolderModFileProvider(staging, expected.Id);
                if (!string.Equals(ModHasher.ComputeInstallSha256(package), download.InstallSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("collection content hash mismatch");
                if (Directory.Exists(target)) Directory.Move(target, backup);
                try { Directory.Move(staging, target); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, target); throw; }
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        /// <summary>Only a single package id may become a directory below the user collection root.</summary>
        public static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id is "." or ".." || id.Contains('/') || id.Contains('\\') || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("invalid collection id");
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':')
                || path.Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("invalid collection package path");
        }
    }
}
