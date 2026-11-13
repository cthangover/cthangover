namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;

    /// <summary>Publishes mod-owned libraries atomically: a shared compiler cache must never expose a half-written mapped DLL.</summary>
    public static class PrecompiledAssemblyCache
    {
        /// <summary>Reuses a verified immutable library or commits a flushed temporary file under the per-key process lease.</summary>
        public static string Stage(string cacheRoot, string modId, string filePath, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modId) || modId.Contains('/') || modId.Contains('\\') || modId is "." or "..")
                throw new ArgumentException("a library cache owner must be a mod id", nameof(modId));
            var hash = SHA256.HashData(bytes);
            var key = Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
            var path = Path.Combine(cacheRoot, $"{modId}_dll_{key}_{Path.GetFileName(filePath)}");
            using var lease = AssemblyCacheEntry.Acquire(path, cancellationToken);
            if (File.Exists(path))
            {
                using var input = File.OpenRead(path);
                if (input.Length != bytes.Length || !SHA256.HashData(input).AsSpan().SequenceEqual(hash))
                    throw new IOException("precompiled library cache hash mismatch: " + path);
                lease.Touch();
                return path;
            }
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes);
                    output.Flush(true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, path);
                lease.Touch();
                return path;
            }
            finally { File.Delete(temporary); }
        }
    }
}
