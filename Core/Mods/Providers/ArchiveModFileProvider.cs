namespace Cthangover.Core.Mods.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using SharpCompress.Archives;
    using SharpCompress.Archives.SevenZip;
    using SharpCompress.Common;
    using SharpCompress.Readers;

    /// <summary>
    /// Shared managed ZIP/7z storage. Independent stream decoders support partial and concurrent reads;
    /// bulk visitors use one sequential decoder instead of repeatedly replaying solid blocks.
    /// Small explicitly buffered reads share a bounded cache; OpenStream never buffers a cache miss.
    /// </summary>
    public abstract class ArchiveModFileProvider : IModFileProvider, IModFileSequence
    {
        private readonly string password;
        private readonly Dictionary<string, int> entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> spellings = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> directories = new(StringComparer.Ordinal);
        private readonly object gate = new();
        private readonly object cacheOwner = new();
        private bool disposed;

        /// <inheritdoc />
        public string Mod { get; }
        /// <inheritdoc />
        public string RootPath { get; }
        /// <summary>Whether any file content is encrypted; encrypted headers are parsed during construction.</summary>
        public bool IsEncrypted { get; }

        /// <summary>Indexes names only. The package must remain unchanged until the provider is disposed.</summary>
        protected ArchiveModFileProvider(string archivePath, string modName, string password)
        {
            RootPath = Path.GetFullPath(archivePath);
            Mod = modName;
            this.password = password;
            using var archive = OpenArchive();
            if (archive.Type != ArchiveType.Zip && archive.Type != ArchiveType.SevenZip)
                throw new InvalidDataException("Only ZIP and 7z packages are supported");
            var index = 0;
            foreach (var entry in archive.Entries)
            {
                var name = ModPath.Normalize(entry.Key);
                if (name.Length > 0 && !(entry is SevenZipArchiveEntry sevenZip && sevenZip.IsAnti))
                {
                    if (!string.IsNullOrEmpty(entry.LinkTarget))
                        throw new InvalidDataException("Archive links are not mod files");
                    if (entry.IsDirectory)
                        directories.Add(name);
                    else
                        entries[name] = index;
                    spellings[name] = name;
                    IsEncrypted |= entry.IsEncrypted;
                }
                index++;
            }
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var name = ModPath.Normalize(path);
            if (entries.ContainsKey(name))
                return true;
            if (spellings.TryGetValue(name, out var actual) && actual != name)
                ModPathCase.ReportMismatch(Mod, name, actual);
            return false;
        }

        /// <inheritdoc />
        public IEnumerable<string> ListFiles(string directory = "")
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var prefix = ModPath.NormalizeDirectory(directory);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in entries.Keys.Concat(directories))
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var suffix = name.Substring(prefix.Length);
                if (suffix.Length == 0 || ModContentFiles.IsHidden(suffix))
                    continue;
                var slash = suffix.IndexOf('/');
                result.Add(slash >= 0 ? prefix + suffix.Substring(0, slash + 1)
                    : directories.Contains(name) ? name + "/" : name);
            }
            return result;
        }

        /// <inheritdoc />
        public string ReadFileText(string path)
        {
            var bytes = ReadFileBinary(path);
            if (bytes == null)
                return null;
            using var stream = new MemoryStream(bytes, false);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <inheritdoc />
        public byte[] ReadFileBinary(string path)
        {
            using var stream = OpenStream(path);
            if (stream == null)
                return null;
            using var output = new MemoryStream();
            stream.CopyTo(output);
            var bytes = output.ToArray();
            lock (gate)
                if (!disposed)
                    ArchiveContentCache.Instance.Remember(cacheOwner, ModPath.Normalize(path), bytes);
            return bytes;
        }

        /// <summary>Returns a readable caller-owned stream; cache misses decode without buffering the whole entry.</summary>
        public Stream OpenStream(string path)
        {
            if (!FileExists(path))
                return null;
            var name = ModPath.Normalize(path);
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                var cached = ArchiveContentCache.Instance.Open(cacheOwner, name);
                if (cached != null)
                    return cached;
            }
            var archive = OpenArchive();
            try
            {
                var entry = archive.Entries.ElementAt(entries[name]);
                return new ArchiveEntryStream(entry.OpenEntryStream(), archive);
            }
            catch
            {
                archive.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public void VisitFiles(Action<string, Stream> visitor, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            using var archive = OpenArchive();
            if (archive.Type == ArchiveType.Zip)
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = ModPath.Normalize(entry.Key);
                    if (entry.IsDirectory || !entries.ContainsKey(path) || ModContentFiles.IsHidden(path))
                        continue;
                    using var input = entry.OpenEntryStream();
                    visitor(path, input);
                }
                return;
            }
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = ModPath.Normalize(reader.Entry.Key);
                if (reader.Entry.IsDirectory || !entries.ContainsKey(name) || ModContentFiles.IsHidden(name))
                    continue;
                using var stream = reader.OpenEntryStream();
                visitor(name, stream);
            }
        }

        /// <summary>Checks an encrypted file to completion, including archives with a plaintext manifest.</summary>
        public void ValidatePassword(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsEncrypted)
                return;
            using var archive = OpenArchive();
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory && entry.IsEncrypted)
                {
                    using var stream = entry.OpenEntryStream();
                    var buffer = new byte[65536];
                    while (stream.Read(buffer, 0, buffer.Length) != 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    return;
                }
            }
        }

        /// <inheritdoc />
        public string GetFileSystemPath(string path) => null;
        /// <inheritdoc />
        public string ResolveIncludePath(string includePath, string currentPath) => ModPath.ResolveRelative(includePath, currentPath);

        /// <summary>Clears owned buffered plaintext. Returned decoder streams remain owned by their callers.</summary>
        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                ArchiveContentCache.Instance.Release(cacheOwner);
            }
        }

        private IArchive OpenArchive() => ArchiveFactory.OpenArchive(RootPath,
            new ReaderOptions { Password = password, LeaveStreamOpen = false });

    }
}
