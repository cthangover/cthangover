namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Incremental content hashing preserving the existing path-plus-content SHA256 format.</summary>
    internal sealed class InstallContentHasher
    {
        private readonly List<string> ignorePatterns;
        private readonly byte[] buffer = new byte[65536];
        private readonly Dictionary<string, byte[]> hashes = new(StringComparer.Ordinal);

        internal InstallContentHasher(List<string> ignorePatterns) => this.ignorePatterns = ignorePatterns;

        internal void Visit(string path, Stream stream)
        {
            path = ModPath.Normalize(path);
            if (ModHasher.ShouldIgnore(path, ignorePatterns))
                return;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            sha.AppendData(Encoding.UTF8.GetBytes(path));
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                sha.AppendData(buffer, 0, count);
            hashes[path] = sha.GetHashAndReset();
        }

        internal string Finish()
        {
            var paths = new List<string>(hashes.Keys);
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in paths)
                sha.AppendData(hashes[path]);
            return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        }
    }
}
