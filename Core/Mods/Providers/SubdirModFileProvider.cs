using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cthangover.Core.Mods.Providers
{
    /// <summary>
    /// A mod-relative sub-folder of another provider seen as a mod root
    /// (test suites of zip mods). Paths are prefixed on the way in and the
    /// prefix is stripped from listings; the inner provider stays owned by
    /// its mod, so <see cref="Dispose"/> does not dispose it.
    /// </summary>
    public sealed class SubdirModFileProvider : IModFileProvider
    {
        private readonly IModFileProvider inner;
        private readonly string prefix;

        public SubdirModFileProvider(IModFileProvider inner, string folder, string modName)
        {
            this.inner = inner;
            prefix = ModPath.Normalize(folder).TrimEnd('/') + "/";
            Mod = modName;
        }

        public string Mod { get; }

        /// <summary>Not disk-backed on its own: a sub-folder of a zip has no root of its own.</summary>
        public string RootPath => null;

        public bool FileExists(string path) => inner.FileExists(Full(path));

        public IEnumerable<string> ListFiles(string directory = "")
            => inner.ListFiles(Full(directory).TrimEnd('/'))
                .Where(e => e.StartsWith(prefix, System.StringComparison.Ordinal))
                .Select(e => e.Substring(prefix.Length));

        public string ReadFileText(string path) => inner.ReadFileText(Full(path));

        public byte[] ReadFileBinary(string path) => inner.ReadFileBinary(Full(path));

        public Stream OpenStream(string path) => inner.OpenStream(Full(path));

        public string GetFileSystemPath(string path) => inner.GetFileSystemPath(Full(path));

        public string ResolveIncludePath(string includePath, string currentPath) => ModPath.ResolveRelative(includePath, currentPath);

        public void Dispose()
        {
        }

        private string Full(string path) => prefix + ModPath.Normalize(path ?? string.Empty);
    }
}
