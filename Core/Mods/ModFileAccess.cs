namespace Cthangover.Core.Mods
{
    using System;
    using System.IO;

    /// <summary>Explicit resource access contracts: readable provider streams, bounded seekable copies, and real cached files.</summary>
    public static class ModFileAccess
    {
        /// <summary>
        /// Returns a caller-owned seekable stream. Forward-only entries use at most memoryLimit bytes
        /// before spilling to a private temporary file deleted on disposal. No full-archive extraction.
        /// </summary>
        public static Stream OpenSeekable(IModFileProvider provider, string path, int memoryLimit = 4 * 1024 * 1024)
        {
            if (memoryLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(memoryLimit));
            var input = provider.OpenStream(path);
            if (input == null || input.CanSeek)
                return input;
            Stream output = new MemoryStream();
            try
            {
                var buffer = new byte[65536];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output is MemoryStream memory && memory.Length + count > memoryLimit)
                    {
                        var disk = Spill(memory);
                        memory.Dispose();
                        output = disk;
                    }
                    if (output is MemoryStream remaining && remaining.Capacity < remaining.Length + count)
                        remaining.Capacity = checked((int)(remaining.Length + count));
                    output.Write(buffer, 0, count);
                }
                output.Position = 0;
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
            finally { input.Dispose(); }
        }

        /// <summary>
        /// Copies only the requested resource to a caller-owned cache location. Publication is atomic;
        /// identical content preserves the existing file/mtime. Returns whether the file changed.
        /// </summary>
        public static bool Materialize(IModFileProvider provider, string path, string destination)
        {
            using var input = provider.OpenStream(path);
            if (input == null)
                throw new FileNotFoundException("Mod resource is missing", path);
            var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            Directory.CreateDirectory(directory);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using (var output = new FileStream(temporary, options))
                    input.CopyTo(output);
                if (File.Exists(destination) && ModHasher.ComputeArchiveSha256(destination) == ModHasher.ComputeArchiveSha256(temporary))
                    return false;
                File.Move(temporary, destination, true);
                return true;
            }
            finally { File.Delete(temporary); }
        }

        private static FileStream Spill(MemoryStream input)
        {
            var path = Path.GetTempFileName();
            FileStream output = null;
            try
            {
                output = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
                    65536, FileOptions.DeleteOnClose);
                input.Position = 0;
                input.CopyTo(output);
                return output;
            }
            catch
            {
                output?.Dispose();
                File.Delete(path);
                throw;
            }
        }
    }
}
