namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Mods.Pipeline;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Utils;
    using SharpCompress.Archives;
    using SharpCompress.Common;
    using SharpCompress.Readers;

    /// <summary>
    /// Prepares ZIP/7z in an isolated sibling directory, validates it before touching the installed
    /// version, and restores the previous directory if publication fails. No Godot or network API.
    /// </summary>
    public static class ModPackageInstaller
    {
        /// <summary>
        /// Installs one package. Hash, extraction, manifest/id or content-hash failures leave the old
        /// installation intact. Passwords are borrowed for this call and never persisted or logged.
        /// </summary>
        public static void Install(string archivePath, string targetDirectory, string expectedModId,
            string archiveSha256 = null, string installSha256 = null, string password = null)
        {
            if (string.IsNullOrWhiteSpace(expectedModId) || expectedModId != Path.GetFileName(expectedModId)
                || expectedModId.Contains('\\') || expectedModId == "." || expectedModId == "..")
                throw new ArgumentException("Invalid mod id", nameof(expectedModId));
            if (!string.IsNullOrEmpty(archiveSha256))
                VerifyHash(archiveSha256, ModHasher.ComputeArchiveSha256(archivePath), "Archive");
            var target = Path.GetFullPath(targetDirectory);
            var parent = Path.GetDirectoryName(target);
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, ".install_" + Guid.NewGuid().ToString("N"));
            var backup = Path.Combine(parent, ".previous_" + Guid.NewGuid().ToString("N"));
            var published = false;
            try
            {
                Directory.CreateDirectory(staging);
                Extract(archivePath, staging, password);
                var content = FindContentRoot(staging);
                using (var provider = new FolderModFileProvider(content, expectedModId))
                {
                    var manifest = ModDiscoveryService.ReadManifest(provider, out var problem);
                    if (manifest == null)
                        throw new InvalidDataException(problem);
                    if (!string.Equals(manifest.Id, expectedModId, StringComparison.Ordinal))
                        throw new InvalidDataException("Package manifest id does not match the requested mod");
                    if (!string.IsNullOrEmpty(installSha256))
                        VerifyHash(installSha256, ModHasher.ComputeInstallSha256(provider,
                            ModConfig.Instance.CatalogIgnorePatterns), "Install");
                }
                if (Directory.Exists(target))
                    Directory.Move(target, backup);
                try
                {
                    Directory.Move(content, target);
                    published = true;
                }
                catch
                {
                    if (Directory.Exists(backup))
                        Directory.Move(backup, target);
                    throw;
                }
            }
            finally
            {
                RemoveTemporary(staging);
                if (published)
                    RemoveTemporary(backup);
            }
        }

        private static void Extract(string path, string staging, string password)
        {
            using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
            if (archive.Type != ArchiveType.Zip && archive.Type != ArchiveType.SevenZip)
                throw new InvalidDataException("Only ZIP and 7z packages are supported");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (archive.Type == ArchiveType.Zip)
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                        continue;
                    using var input = entry.OpenEntryStream();
                    ExtractFile(entry, input, staging, names);
                }
                return;
            }
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                    continue;
                using var input = reader.OpenEntryStream();
                ExtractFile(reader.Entry, input, staging, names);
            }
        }

        private static void ExtractFile(IEntry entry, Stream input, string staging, HashSet<string> names)
        {
            var name = entry.Key?.Replace('\\', '/') ?? "";
            if (name.Length == 0 || name.StartsWith('/') || name.Contains(':') || !string.IsNullOrEmpty(entry.LinkTarget)
                || (entry.Attrib.GetValueOrDefault() & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Unsafe archive entry");
            foreach (var segment in name.Split('/'))
                if (segment == ".." || segment == ".")
                    throw new InvalidDataException("Archive path escapes the package");
            if (!names.Add(name))
                throw new InvalidDataException("Duplicate or case-ambiguous archive path");
            var destination = Path.GetFullPath(Path.Combine(staging, name));
            if (!destination.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Archive path escapes the staging directory");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using var output = File.Open(destination, FileMode.CreateNew, FileAccess.Write);
            input.CopyTo(output);
        }

        private static string FindContentRoot(string staging)
        {
            if (File.Exists(Path.Combine(staging, "manifest.json")))
                return staging;
            var entries = Directory.GetFileSystemEntries(staging);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
                return entries[0];
            throw new InvalidDataException("Package does not contain a root manifest.json");
        }

        private static void VerifyHash(string expected, string actual, string kind)
        {
            if (!string.IsNullOrEmpty(expected) && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(kind + " SHA256 mismatch");
        }

        private static void RemoveTemporary(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                GameLogger.Log("DOWNLOADER", "Cannot remove package staging/backup: " + ex.Message, LogLevel.Warning);
            }
        }
    }
}
