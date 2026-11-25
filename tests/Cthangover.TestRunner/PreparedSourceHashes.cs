namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Memoizes live source hashes across scans and, after a successful admission, across runner invocations.</summary>
    internal sealed class PreparedSourceHashes
    {
        private readonly string path;
        private readonly InputPreparationMetrics metrics;
        private readonly string cachedFingerprint;
        private Dictionary<string, Entry> previous;
        private Dictionary<string, Entry> current;
        private long stableBefore;
        private bool dirty;

        internal PreparedSourceHashes(string path, string readyFingerprint, InputPreparationMetrics metrics)
        {
            this.path = path;
            this.metrics = metrics;
            if (path == null)
            {
                return;
            }

            var receipt = Load(path);
            if (readyFingerprint != null && receipt?.Fingerprint == readyFingerprint && receipt.Files != null)
            {
                cachedFingerprint = readyFingerprint;
                previous = receipt.Files;
            }
            else
            {
                previous = new Dictionary<string, Entry>(StringComparer.Ordinal);
            }
        }

        internal void BeginScan()
        {
            if (path != null)
            {
                // FAT write timestamps may have two-second resolution. Rehash recent writes even on a metadata hit.
                stableBefore = DateTime.UtcNow.AddSeconds(-2).Ticks;
                current = new Dictionary<string, Entry>(previous.Count, StringComparer.Ordinal);
            }
        }

        internal PreparedInputFile Read(string root, string file, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (path == null)
            {
                return ReadContent(root, file, cancellation);
            }

            PreparedInputFiles.RejectLink(file);
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var info = new FileInfo(file);
            var length = info.Length;
            var modified = info.LastWriteTimeUtc.Ticks;
            if (modified < stableBefore && previous.TryGetValue(relative, out var cached)
                && cached.Length == length && cached.Modified == modified && cached.Hash?.Length == 64)
            {
                metrics.SourceHashesReused++;
                current.Add(relative, cached);
                return new PreparedInputFile { Path = relative, Length = length, Hash = cached.Hash };
            }

            var input = ReadContent(root, file, cancellation);
            info.Refresh();
            if (length != input.Length || length != info.Length || modified != info.LastWriteTimeUtc.Ticks)
            {
                throw new IOException("Input changed while hashing: " + file);
            }

            if (modified < stableBefore)
            {
                current.Add(relative, new Entry(length, modified, input.Hash));
                dirty = true;
            }
            return input;
        }

        internal void EndScan()
        {
            if (path != null)
            {
                dirty |= previous.Count != current.Count;
                previous = current;
                current = null;
            }
        }

        internal void Save(string fingerprint)
        {
            if (path == null || !dirty && cachedFingerprint == fingerprint)
            {
                return;
            }

            try
            {
                PreparedInputJson.Write(path, new Receipt(fingerprint, previous));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private PreparedInputFile ReadContent(string root, string file, CancellationToken cancellation)
        {
            var input = PreparedInputFiles.Read(root, file, cancellation);
            metrics.SourceFilesHashed++;
            metrics.SourceBytesHashed += input.Length;
            return input;
        }

        private static Receipt Load(string path)
        {
            try
            {
                return PreparedInputJson.Read<Receipt>(path);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (JsonException) { return null; }
        }

        private readonly record struct Entry(long Length, long Modified, string Hash);

        private sealed record Receipt(string Fingerprint, Dictionary<string, Entry> Files);
    }
}
