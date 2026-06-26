using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// Mod file provider backed by a real directory on disk. All paths
    /// are relative to the mod root; directory separators are normalised
    /// to the OS-native character. Trailing <c>/</c> in <c>ListFiles</c>
    /// output distinguishes directories from files so that recursive
    /// scanners can branch without stat-calling every entry.
    ///
    /// <c>GetFileSystemPath</c> returns the absolute path — the critical
    /// capability that enables Godot's <c>ResourceLoader</c> and
    /// <c>PackedScene</c> instantiation from folder mods. Zip providers
    /// return <c>null</c> here, making this the only provider type that
    /// supports scene-based assets.
    ///
    /// <c>Dispose</c> is a no-op because directories don't need cleanup.
    /// </summary>
    public class FolderModFileProvider : IModFileProvider
    {
        /// <inheritdoc />
        public string Mod { get; }
        private readonly string rootPath;

        /// <summary>
        /// Creates a provider rooted at the given directory.
        /// <paramref name="modName"/> becomes the <see cref="Mod"/> value.
        /// </summary>
        public FolderModFileProvider(string rootPath, string modName)
        {
            this.rootPath = rootPath;
            this.Mod = modName;
        }

        public void Dispose()
        { }

        public bool FileExists(string path)
        {
            return File.Exists(Path.Combine(rootPath, NormalizePath(path)));
        }

        public IEnumerable<string> ListFiles(string directory = "")
        {
            var dir = Path.Combine(rootPath, NormalizePath(directory));
            if (!Directory.Exists(dir))
                yield break;

            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var relative = Path.GetRelativePath(rootPath, entry).Replace('\\', '/');
                if (Directory.Exists(entry))
                    relative += "/";
                yield return relative;
            }
        }

        public string ReadFileText(string path)
        {
            var fullPath = Path.Combine(rootPath, NormalizePath(path));
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }

        public byte[] ReadFileBinary(string path)
        {
            var fullPath = Path.Combine(rootPath, NormalizePath(path));
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }

        public Stream OpenStream(string path)
        {
            var fullPath = Path.Combine(rootPath, NormalizePath(path));
            if (!File.Exists(fullPath))
                return null;
            return File.OpenRead(fullPath);
        }

        public string GetFileSystemPath(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(rootPath, NormalizePath(path)));
            return File.Exists(fullPath) ? fullPath : null;
        }

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
