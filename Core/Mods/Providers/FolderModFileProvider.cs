using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// Mod file provider backed by a real directory on disk. All paths
    /// are relative to the mod root and use the canonical form defined by
    /// <see cref="ModPath"/> (forward slashes, no root or <c>..</c> escape);
    /// conversion to the OS-native separator happens only at the
    /// <see cref="System.IO.Path"/> boundary. Trailing <c>/</c> in
    /// <see cref="ListFiles"/> output distinguishes directories from files so
    /// that recursive scanners can branch without stat-calling every entry.
    ///
    /// <see cref="GetFileSystemPath"/> returns the absolute path — the
    /// critical capability that enables Godot's <c>ResourceLoader</c> and
    /// <c>PackedScene</c> instantiation from folder mods. Zip providers
    /// return <c>null</c> here, making this the only provider type that
    /// supports scene-based assets.
    ///
    /// <see cref="Dispose"/> is a no-op because directories don't need
    /// cleanup.
    ///
    /// Lookups follow <see cref="ModPathCase"/>: a path must match the
    /// on-disk spelling exactly on every OS, so a wrong-case reference fails
    /// on the author's Windows machine the same way it fails on Linux and in
    /// zip mods. Verified spellings are cached to keep repeated lookups cheap;
    /// entries returned by <see cref="ListFiles"/> come straight from the
    /// directory, so they count as verified (a scan that lists and then reads
    /// does not walk the path again — on slow file systems that walk dominated
    /// boot).
    /// </summary>
    public class FolderModFileProvider : IModFileProvider
    {
        /// <inheritdoc />
        public string Mod { get; }

        /// <inheritdoc />
        public string RootPath { get; }

        private readonly ConcurrentDictionary<string, byte> verifiedFiles = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> verifiedDirs = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates a provider rooted at the given directory.
        /// <paramref name="modName"/> becomes the <see cref="Mod"/> value
        /// used in log messages and lookup keys.
        /// </summary>
        /// <param name="rootPath">Absolute or relative path to the mod
        /// root directory on disk.</param>
        /// <param name="modName">Short identifier for this mod.</param>
        public FolderModFileProvider(string rootPath, string modName)
        {
            this.RootPath = rootPath;
            this.Mod = modName;
        }

        /// <summary>
        /// No-op: directories do not hold native handles and need no
        /// explicit cleanup.
        /// </summary>
        public void Dispose()
        { }

        /// <summary>
        /// Checks whether a file exists at the given mod-relative path
        /// with exactly this spelling (see <see cref="ModPathCase"/>).
        /// </summary>
        public bool FileExists(string path)
        {
            return ResolveFile(path) != null;
        }

        /// <summary>
        /// Returns a shallow listing of the given directory inside the
        /// mod. Directories are suffixed with <c>/</c> so that recursive
        /// callers can distinguish them from files without additional
        /// stat calls.
        /// </summary>
        /// <param name="directory">Mod-relative directory path. Empty
        /// string for the mod root.</param>
        public IEnumerable<string> ListFiles(string directory = "")
        {
            var relativeDir = ModPath.Normalize(directory);
            var dir = Path.Combine(RootPath, relativeDir);
            if (!verifiedDirs.ContainsKey(relativeDir))
            {
                var actualDir = ModPathCase.FindOnDisk(RootPath, relativeDir, directory: true);
                if (actualDir == null)
                    yield break;

                if (!string.Equals(actualDir, relativeDir, StringComparison.Ordinal))
                {
                    ModPathCase.ReportMismatch(Mod, relativeDir, actualDir);
                    yield break;
                }

                verifiedDirs.TryAdd(relativeDir, 0);
            }
            else if (!Directory.Exists(dir))
                yield break;

            foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                if (ModContentFiles.IsHidden(entry.Name))
                    continue;

                var relative = ModPath.Normalize(Path.GetRelativePath(RootPath, entry.FullName));
                if (entry is DirectoryInfo)
                {
                    verifiedDirs.TryAdd(relative, 0);
                    relative += "/";
                }
                else
                    verifiedFiles.TryAdd(relative, 0);
                yield return relative;
            }
        }

        /// <summary>
        /// Reads the full contents of a text file at the given
        /// mod-relative path as a UTF-8 string. Returns <c>null</c>
        /// if the file does not exist.
        /// </summary>
        public string ReadFileText(string path)
        {
            var fullPath = ResolveFile(path);
            return fullPath != null ? File.ReadAllText(fullPath) : null;
        }

        /// <summary>
        /// Reads the full binary content of a file inside the mod.
        /// Returns <c>null</c> if the file does not exist.
        /// </summary>
        public byte[] ReadFileBinary(string path)
        {
            var fullPath = ResolveFile(path);
            return fullPath != null ? File.ReadAllBytes(fullPath) : null;
        }

        /// <summary>
        /// Opens a read-only <see cref="Stream"/> for the given
        /// mod-relative path. Returns <c>null</c> if the file does
        /// not exist. The caller is responsible for disposing the stream.
        /// </summary>
        public Stream OpenStream(string path)
        {
            var fullPath = ResolveFile(path);
            return fullPath != null ? File.OpenRead(fullPath) : null;
        }

        /// <summary>
        /// Returns the absolute filesystem path for the given
        /// mod-relative path, or <c>null</c> if the file does not exist.
        /// This is the only provider type that returns non-null here,
        /// which makes folder mods the only mods capable of serving
        /// Godot scenes via <c>ResourceLoader</c>.
        /// </summary>
        public string GetFileSystemPath(string path)
        {
            var fullPath = ResolveFile(path);
            return fullPath != null ? Path.GetFullPath(fullPath) : null;
        }

        /// <summary>
        /// Resolves a relative include path (<c>../shared/data.json</c>)
        /// against the directory of <paramref name="currentPath"/> to
        /// produce a canonical mod-relative path. Delegates to
        /// <see cref="ModPath.ResolveRelative"/> so folder and zip mods
        /// resolve includes identically.
        /// </summary>
        public string ResolveIncludePath(string includePath, string currentPath)
        {
            return ModPath.ResolveRelative(includePath, currentPath);
        }

        /// <summary>
        /// Returns the full path of an existing file whose spelling matches
        /// <paramref name="path"/> exactly, or <c>null</c>. A case-only
        /// mismatch is reported once and treated as a miss.
        /// </summary>
        private string ResolveFile(string path)
        {
            var relative = ModPath.Normalize(path);
            var fullPath = Path.Combine(RootPath, relative);
            if (verifiedFiles.ContainsKey(relative))
                return File.Exists(fullPath) ? fullPath : null;

            var actual = ModPathCase.FindOnDisk(RootPath, relative, directory: false);
            if (actual == null)
                return null;

            if (!string.Equals(actual, relative, StringComparison.Ordinal))
            {
                ModPathCase.ReportMismatch(Mod, relative, actual);
                return null;
            }

            verifiedFiles.TryAdd(relative, 0);
            return fullPath;
        }
    }
}
