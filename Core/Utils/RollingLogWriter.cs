using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Owns one append stream, flushed after each LF-terminated UTF-8 record.
    /// The caller serializes access (GameLogger uses its existing lock). Archives
    /// append .1, .2, etc. to the full path; MaxFiles includes the active file.
    /// Rotation failures keep the current file writable; other I/O failures drop
    /// that record and allow the next record to retry opening the sink.
    /// </summary>
    public sealed class RollingLogWriter : IDisposable
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string path;
        private readonly long maxBytes;
        private readonly int maxFiles;
        private StreamWriter writer;
        private long bytes;
        private bool disposed;
        /// <summary>
        /// Opens immediately and rotates an existing file at or above the limit.
        /// Byte limits may be small for callers testing rotation; configuration
        /// in megabytes is validated separately. A record larger than the limit
        /// remains intact in one file and rotates before the following record.
        /// </summary>
        public RollingLogWriter(string path, long maxFileSizeBytes, int maxFiles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (maxFileSizeBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));
            }

            if (maxFiles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFiles));
            }

            this.path = path;
            maxBytes = maxFileSizeBytes;
            this.maxFiles = maxFiles;
            PruneArchives();
            Open();
            if (writer != null && bytes >= maxBytes)
            {
                Rotate();
            }
        }

        /// <summary>
        /// Flushes a whole record before returning. Rotation occurs before a
        /// nonempty active file would exceed the UTF-8 byte budget, without
        /// querying file metadata on each call. Writes after disposal are ignored.
        /// </summary>
        public void WriteLine(string line)
        {
            if (disposed)
            {
                return;
            }

            if (writer == null)
            {
                Open();
            }

            if (writer == null)
            {
                return;
            }

            var size = (long)Utf8.GetByteCount(line ?? string.Empty) + 1;
            if (bytes > 0 && (bytes >= maxBytes || size > maxBytes - bytes))
            {
                Rotate();
            }

            if (writer == null)
            {
                return;
            }

            try
            {
                writer.WriteLine(line);
                bytes += size;
            }
            catch (IOException)
            {
                Close();
            }
            catch (UnauthorizedAccessException)
            {
                Close();
            }
        }

        private void Open()
        {
            FileStream stream = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                bytes = stream.Length;
                writer = new StreamWriter(stream, Utf8)
                {
                    NewLine = "\n",
                    AutoFlush = true
                };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                stream?.Dispose();
                writer = null;
            }
        }

        private string Archive(int index)
        {
            return path + "." + index.ToString(CultureInfo.InvariantCulture);
        }

        private void Rotate()
        {
            Close();
            try
            {
                if (maxFiles == 1)
                {
                    File.Delete(path);
                }
                else
                {
                    File.Delete(Archive(maxFiles - 1));
                    for (var i = maxFiles - 2; i >= 1; i--)
                    {
                        if (File.Exists(Archive(i)))
                        {
                            File.Move(Archive(i), Archive(i + 1));
                        }
                    }

                    File.Move(path, Archive(1));
                }
            }
            catch (IOException)
            {
                // Reopen the current file after a failed rename.
            }
            catch (UnauthorizedAccessException)
            {
            }

            Open();
        }

        private void PruneArchives()
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath)!;
                if (!Directory.Exists(directory))
                {
                    return;
                }

                var prefix = Path.GetFileName(fullPath) + ".";
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith(prefix, StringComparison.Ordinal)
                        && int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                        && index >= maxFiles)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        private void Close()
        {
            try
            {
                writer?.Dispose();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                writer = null;
            }
        }

        /// <summary>
        /// Closes the owned stream once; never reopens it during shutdown.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Close();
        }
    }
}
