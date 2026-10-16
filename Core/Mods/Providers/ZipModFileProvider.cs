using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// Mod file provider backed by a <c>.zip</c> archive. On construction
    /// the entire zip entry table is flattened into an exact-case
    /// dictionary — this trades startup memory for O(1) lookups on every
    /// subsequent file access call, which dominates because factories
    /// repeatedly check file existence during cache-population scans.
    ///
    /// <see cref="ListFiles"/> simulates a shallow directory listing by
    /// parsing the flat zip entry list: entries whose relative path
    /// contains a <c>/</c> after the prefix become synthetic directory
    /// entries (trailing <c>/</c>), while entries at the prefix level
    /// become file entries. This preserves the same recursive-walk
    /// interface that <see cref="FolderModFileProvider"/> provides natively.
    ///
    /// <see cref="GetFileSystemPath"/> always returns <c>null</c> — Godot
    /// cannot load resources or scenes directly from within a zip, so
    /// callers that need filesystem paths (e.g. <c>EffectFactory</c>,
    /// shader loading) must extract files to the cache or skip zip mods
    /// entirely.
    ///
    /// Lookups follow <see cref="ModPathCase"/>: the spelling must match the
    /// archive entry exactly, like folder mods on Linux; a case-only
    /// mismatch is reported with the correct path and treated as a miss.
    /// </summary>
    public class ZipModFileProvider : IModFileProvider
    {
        private readonly ZipArchive archive;
        private readonly Dictionary<string, ZipArchiveEntry> entryLookup;
        private readonly Dictionary<string, string> spellingLookup;

        /// <inheritdoc />
        public string Mod { get; }

        /// <inheritdoc />
        public string RootPath { get; }
        
        /// <summary>
        /// Opens the zip archive at <paramref name="zipPath"/> and builds
        /// an O(1) exact-case lookup dictionary from every entry's
        /// full path. The zip remains open until <see cref="Dispose"/> is
        /// called — callers must dispose the provider to release the
        /// native file handle.
        /// </summary>
        /// <param name="zipPath">Absolute or relative path to the
        /// <c>.zip</c> file.</param>
        /// <param name="modName">Short identifier for this mod, used in
        /// log messages and as the <see cref="Mod"/> value.</param>
        public ZipModFileProvider(string zipPath, string modName)
        {
            this.RootPath = zipPath;
            this.Mod = modName;
            archive = ZipFile.OpenRead(zipPath);
            entryLookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            spellingLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in archive.Entries)
            {
                var name = ModPath.Normalize(e.FullName);
                if (name.Length > 0)
                {
                    entryLookup[name] = e;
                    spellingLookup[name] = name;
                }
            }
            GameLogger.Log("MODS_ZIP", $"Opened zip '{Path.GetFileName(zipPath)}': {entryLookup.Count} entries total");
            foreach (var e in archive.Entries)
                GameLogger.Log("MODS_ZIP", $"  entry: '{e.FullName}' length={e.Length}", LogLevel.Debug);
        }

        /// <summary>
        /// Closes the underlying <see cref="ZipArchive"/> and releases
        /// the native file handle. After disposal, all read operations
        /// will fail.
        /// </summary>
        public void Dispose()
        {
            archive?.Dispose();
        }

        /// <summary>
        /// Checks whether the given path exists as a zip entry using the
        /// exact-case lookup dictionary built during construction.
        /// </summary>
        public bool FileExists(string path)
        {
            return TryGetEntry(ModPath.Normalize(path), out _);
        }

        /// <summary>
        /// Returns a simulated shallow directory listing by grouping zip
        /// entries. Entries at the given directory level become file
        /// entries; entries one level deeper become synthetic directory
        /// entries suffixed with <c>/</c>. This preserves the same
        /// recursive-walk contract that <see cref="FolderModFileProvider"/>
        /// provides natively.
        /// </summary>
        /// <param name="directory">Mod-relative directory path. Empty
        /// string for the archive root.</param>
        public IEnumerable<string> ListFiles(string directory = "")
        {
            var prefix = ModPath.NormalizeDirectory(directory);

            var seen = new HashSet<string>();
            var yieldList = new List<string>();

            foreach (var entry in archive.Entries)
            {
                var rawName = entry.FullName.Replace('\\', '/');
                var isDir = rawName.EndsWith("/");
                var fullName = ModPath.Normalize(rawName);
                if (string.IsNullOrEmpty(prefix) || fullName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var suffix = string.IsNullOrEmpty(prefix)
                        ? fullName
                        : fullName.Substring(prefix.Length);

                    if (string.IsNullOrEmpty(suffix))
                        continue;

                    var idx = suffix.IndexOf('/');
                    if (idx >= 0)
                    {
                        var dirPart = prefix + suffix.Substring(0, idx + 1);
                        if (seen.Add(dirPart))
                            yieldList.Add(dirPart);
                    }
                    else if (isDir)
                    {
                        var dirPart = prefix + suffix + "/";
                        if (seen.Add(dirPart))
                            yieldList.Add(dirPart);
                    }
                    else
                    {
                        var filePath = prefix + suffix;
                        if (seen.Add(filePath))
                            yieldList.Add(filePath);
                    }
                }
            }
            foreach (var item in yieldList)
                yield return item;
        }

        /// <summary>
        /// Reads the full text content of a zip entry as a UTF-8 string.
        /// Returns <c>null</c> if the entry does not exist.
        /// </summary>
        public string ReadFileText(string path)
        {
            var normalized = ModPath.Normalize(path);
            if (!TryGetEntry(normalized, out var entry))
                return null;
            GameLogger.Log("MODS_ZIP", $"ReadFileText('{path}') norm='{normalized}' => found ({entry.Length} bytes)");
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Reads the full binary content of a zip entry. Returns
        /// <c>null</c> if the entry does not exist.
        /// </summary>
        public byte[] ReadFileBinary(string path)
        {
            var normalized = ModPath.Normalize(path);
            if (!TryGetEntry(normalized, out var entry))
                return null;

            GameLogger.Log("MODS_ZIP", $"ReadFileBinary('{path}') norm='{normalized}' => found ({entry.Length} bytes)");

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Opens a read-only stream to the zip entry. Returns <c>null</c>
        /// if the entry does not exist. The caller is responsible for
        /// disposing the stream.
        /// </summary>
        public Stream OpenStream(string path)
        {
            var normalized = ModPath.Normalize(path);
            if (!TryGetEntry(normalized, out var entry))
                return null;
            
            GameLogger.Log("MODS_ZIP", $"OpenStream('{path}') norm='{normalized}' => found");
            return entry.Open();
        }

        /// <summary>
        /// Always returns <c>null</c>. Zip mods cannot provide filesystem
        /// paths — Godot's <c>ResourceLoader</c> requires real filesystem
        /// paths, so callers dependent on scene instantiation must use
        /// folder mods or extract assets to the cache first.
        /// </summary>
        public string GetFileSystemPath(string path)
        {
            return null;
        }

        /// <summary>
        /// Resolves a relative include path (<c>../shared/data.json</c>)
        /// against the directory of <paramref name="currentPath"/> to
        /// produce a canonical mod-relative path. Delegates to
        /// <see cref="ModPath.ResolveRelative"/> so zip and folder mods
        /// resolve includes identically.
        /// </summary>
        public string ResolveIncludePath(string includePath, string currentPath)
        {
            var result = ModPath.ResolveRelative(includePath, currentPath);
            GameLogger.Log("MODS_ZIP", $"ResolveIncludePath(include='{includePath}', current='{currentPath}') => '{result}'");
            return result;
        }

        /// <summary>
        /// Exact-case entry lookup; a case-only mismatch is reported through
        /// <see cref="ModPathCase.ReportMismatch"/> and treated as a miss.
        /// </summary>
        private bool TryGetEntry(string normalized, out ZipArchiveEntry entry)
        {
            if (entryLookup.TryGetValue(normalized, out entry))
                return true;

            if (spellingLookup.TryGetValue(normalized, out var actual))
                ModPathCase.ReportMismatch(Mod, normalized, actual);

            return false;
        }
    }
}
