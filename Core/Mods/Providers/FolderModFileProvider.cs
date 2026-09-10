using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// Mod file provider backed by a real directory on disk. All paths
    /// are relative to the mod root; directory separators are normalised
    /// to the OS-native character. Trailing <c>/</c> in <see cref="ListFiles"/>
    /// output distinguishes directories from files so that recursive
    /// scanners can branch without stat-calling every entry.
    ///
    /// <see cref="GetFileSystemPath"/> returns the absolute path — the
    /// critical capability that enables Godot's <c>ResourceLoader</c> and
    /// <c>PackedScene</c> instantiation from folder mods. Zip providers
    /// return <c>null</c> here, making this the only provider type that
    /// supports scene-based assets.
    ///
    /// <see cref="Dispose"/> is a no-op because directories don't need
    /// cleanup.
    /// </summary>
    public class FolderModFileProvider : IModFileProvider
    {
        /// <inheritdoc />
        public string Mod { get; }

        /// <inheritdoc />
        public string RootPath { get; }

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
        /// by combining it with <see cref="RootPath"/> and testing
        /// <see cref="File.Exists(string)"/>.
        /// </summary>
        public bool FileExists(string path)
        {
            return File.Exists(Path.Combine(RootPath, NormalizePath(path)));
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
            var dir = Path.Combine(RootPath, NormalizePath(directory));
            if (!Directory.Exists(dir))
                yield break;

            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var relative = Path.GetRelativePath(RootPath, entry).Replace('\\', '/');
                if (Directory.Exists(entry))
                    relative += "/";
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
            var fullPath = Path.Combine(RootPath, NormalizePath(path));
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }

        /// <summary>
        /// Reads the full binary content of a file inside the mod.
        /// Returns <c>null</c> if the file does not exist.
        /// </summary>
        public byte[] ReadFileBinary(string path)
        {
            var fullPath = Path.Combine(RootPath, NormalizePath(path));
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }

        /// <summary>
        /// Opens a read-only <see cref="Stream"/> for the given
        /// mod-relative path. Returns <c>null</c> if the file does
        /// not exist. The caller is responsible for disposing the stream.
        /// </summary>
        public Stream OpenStream(string path)
        {
            var fullPath = Path.Combine(RootPath, NormalizePath(path));
            if (!File.Exists(fullPath))
                return null;
            return File.OpenRead(fullPath);
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
            var fullPath = Path.GetFullPath(Path.Combine(RootPath, NormalizePath(path)));
            return File.Exists(fullPath) ? fullPath : null;
        }

        /// <summary>
        /// Resolves a relative include path (<c>../shared/data.json</c>)
        /// against the directory of <paramref name="currentPath"/> to
        /// produce a canonical mod-relative path.
        /// </summary>
        public string ResolveIncludePath(string includePath, string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
                return includePath;

            var lastSlash = currentPath.LastIndexOf('/');
            var baseDir = lastSlash < 0 ? "" : currentPath.Substring(0, lastSlash);
            return string.IsNullOrEmpty(baseDir) ? includePath : baseDir + "/" + includePath;
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('/', Path.DirectorySeparatorChar) ?? "";
        }
    }
}
