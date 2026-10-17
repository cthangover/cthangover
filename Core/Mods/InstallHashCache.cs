namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Process-wide cache of <see cref="ModInfo.InstallSha256"/>. The install
    /// hash reads every file of a mod (music included), and discovery computes
    /// it for every installed mod — at boot that is acceptable, but an
    /// in-process reload (game switch, hot reload) rediscovers everything and
    /// spent most of its time re-hashing unchanged mods. A cheap stamp of the
    /// mod's files (relative path, size and modification time of every file in
    /// a folder mod; size and time of a zip) decides whether the cached hash
    /// still applies; any change recomputes it. A provider whose root is not a
    /// file or folder on disk is always hashed.
    /// </summary>
    public static class InstallHashCache
    {
        private static readonly ConcurrentDictionary<string, (string Stamp, string Hash)> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The install hash of <paramref name="provider"/>'s mod, reused while its files are unchanged.</summary>
        public static string Get(IModFileProvider provider, List<string> ignorePatterns)
        {
            var root = provider?.RootPath;
            var stamp = root != null ? Stamp(root, ignorePatterns) : null;
            if (stamp != null && Cache.TryGetValue(root, out var cached) && cached.Stamp == stamp)
                return cached.Hash;

            var hash = ModHasher.ComputeInstallSha256(provider, ignorePatterns);
            if (stamp != null)
                Cache[root] = (stamp, hash);
            return hash;
        }

        /// <summary>
        /// The change stamp of a mod root: a folder's file list with sizes and
        /// modification times, or a zip's size and time; null when the root
        /// does not exist.
        /// </summary>
        public static string Stamp(string root, List<string> ignorePatterns = null)
        {
            var text = new StringBuilder(string.Join("|", ignorePatterns ?? new List<string>()));
            if (File.Exists(root))
            {
                var info = new FileInfo(root);
                return text.Append('#').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks).ToString();
            }
            if (!Directory.Exists(root))
                return null;

            var files = new List<string>(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                text.Append('\n').Append(Path.GetRelativePath(root, file)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }
}
