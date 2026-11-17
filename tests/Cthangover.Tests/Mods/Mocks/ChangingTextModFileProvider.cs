namespace Engine.Tests.Mods.Mocks
{
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Mods;

    /// <summary>A borrowed real provider with one deliberately unstable resource exercises coherent snapshot refusal.</summary>
    internal sealed class ChangingTextModFileProvider : IModFileProvider
    {
        private readonly IModFileProvider source;
        private readonly string changing;
        private int reads;

        internal ChangingTextModFileProvider(IModFileProvider source, string changing)
        {
            this.source = source;
            this.changing = changing;
        }

        /// <inheritdoc />
        public string Mod => source.Mod;

        /// <inheritdoc />
        public string RootPath => source.RootPath;

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            return source.FileExists(path);
        }

        /// <inheritdoc />
        public IEnumerable<string> ListFiles(string directory = "")
        {
            return source.ListFiles(directory);
        }

        /// <inheritdoc />
        public string ReadFileText(string path)
        {
            if (path == changing)
            {
                reads++;
                return reads == 1 ? "ptext first" : "ptext second";
            }

            return source.ReadFileText(path);
        }

        /// <inheritdoc />
        public byte[] ReadFileBinary(string path)
        {
            return source.ReadFileBinary(path);
        }

        /// <inheritdoc />
        public Stream OpenStream(string path)
        {
            return source.OpenStream(path);
        }

        /// <inheritdoc />
        public string GetFileSystemPath(string path)
        {
            return source.GetFileSystemPath(path);
        }

        /// <inheritdoc />
        public string ResolveIncludePath(string includePath, string currentPath)
        {
            return source.ResolveIncludePath(includePath, currentPath);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
