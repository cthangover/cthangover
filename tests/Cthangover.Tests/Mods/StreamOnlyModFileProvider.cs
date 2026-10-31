namespace Engine.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;

    /// <summary>A real folder whose public contract deliberately rejects whole-file binary/text buffering.</summary>
    internal sealed class StreamOnlyModFileProvider : IModFileProvider
    {
        private readonly FolderModFileProvider inner;
        internal StreamOnlyModFileProvider(string root) => inner = new FolderModFileProvider(root, "stream_only");
        public string Mod => inner.Mod;
        public string RootPath => inner.RootPath;
        public bool FileExists(string path) => inner.FileExists(path);
        public IEnumerable<string> ListFiles(string directory = "") => inner.ListFiles(directory);
        public Stream OpenStream(string path) => inner.OpenStream(path);
        public string ReadFileText(string path) => throw new InvalidOperationException("Use streams");
        public byte[] ReadFileBinary(string path) => throw new InvalidOperationException("Use streams");
        public string GetFileSystemPath(string path) => null;
        public string ResolveIncludePath(string include, string current) => inner.ResolveIncludePath(include, current);
        public void Dispose() => inner.Dispose();
    }
}
