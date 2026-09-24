using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// SHA256 checksum utility for mod catalog export. Two hash modes:
    ///
    /// <b>archive_sha256</b> — hash of the entire .zip file as it sits
    /// on the server. Used after download to verify file integrity
    /// (no corruption during transfer).
    ///
    /// <b>install_sha256</b> — hash of the mod's extracted content,
    /// computed by enumerating all files, sorting by relative path,
    /// hashing each as <c>SHA256(UTF8(path) + content)</c>, then
    /// hashing the concatenation of those per-file hashes. This
    /// produces a reproducible fingerprint that the game can compare
    /// against the locally-installed copy: if the local hash differs,
    /// the player has modified the mod and overwriting from the
    /// network may lose their changes.
    ///
    /// Files whose path contains any component starting with a dot
    /// (<c>.godot/</c>, <c>.import/</c>, <c>.gitignore</c>) are
    /// automatically excluded. Additional patterns from
    /// <see cref="ModConfig.CatalogIgnorePatterns"/> are also applied.
    /// </summary>
    public static class ModHasher
    {
        /// <summary>
        /// Returns lowercase hex-encoded SHA256 of the file at
        /// <paramref name="filePath"/>.
        /// </summary>
        public static string ComputeArchiveSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BytesToHex(hash);
        }

        /// <summary>
        /// Computes a short SHA-256 hash from a set of source files for mod compilation caching.
        /// Concatenates each file's path and source text (ordered by path for determinism),
        /// then returns the first 16 hex characters of the SHA-256 hash.
        /// Used by <see cref="ModCompiler"/> to determine whether a mod needs recompilation.
        /// <paramref name="dependencySignature"/> is mixed into the hash so that a change in
        /// the mod's dependencies — for example renaming a dependency mod, which changes the
        /// assembly name its compiled code links against — invalidates the cache even when
        /// the mod's own sources are unchanged.
        /// </summary>
        public static string ComputeHash(List<(string FilePath, string Source)> sources, string dependencySignature = null)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (var (path, source) in sources.OrderBy(s => s.FilePath))
            {
                sb.Append(path);
                sb.Append(':');
                sb.Append(source);
            }

            if (!string.IsNullOrEmpty(dependencySignature))
            {
                sb.Append("#deps:");
                sb.Append(dependencySignature);
            }

            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hashBytes).Substring(0, 16).ToLowerInvariant();
        }
        
        /// <summary>
        /// Computes the reproducible install content hash from a folder
        /// on disk. Files are enumerated recursively; directories and
        /// entries matching ignore patterns are skipped.
        /// </summary>
        public static string ComputeInstallSha256(string folderPath, List<string> ignorePatterns = null)
        {
            var files = new List<string>(
                Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories));

            return ComputeInstallSha256FromFiles(folderPath, files, ignorePatterns);
        }

        /// <summary>
        /// Computes the reproducible install content hash through an
        /// <see cref="IModFileProvider"/>. Works for both folder and zip
        /// providers. Recursively collects all files via
        /// <see cref="IModFileProvider.ListFiles"/>.
        /// </summary>
        public static string ComputeInstallSha256(IModFileProvider provider, List<string> ignorePatterns = null)
        {
            var allFiles = CollectAllFilesRecursive(provider, "");
            return ComputeInstallSha256FromProvider(provider, allFiles, ignorePatterns);
        }

        private static List<string> CollectAllFilesRecursive(IModFileProvider provider, string directory)
        {
            var result = new List<string>();
            var entries = provider.ListFiles(directory);

            foreach (var entry in entries)
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry;
                    result.AddRange(CollectAllFilesRecursive(provider, subDir));
                }
                else
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        private static string ComputeInstallSha256FromProvider(
            IModFileProvider provider, List<string> files, List<string> ignorePatterns)
        {
            var sorted = files
                .Select(ModPath.Normalize)
                .Where(f => !ShouldIgnore(f, ignorePatterns))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using var sha = SHA256.Create();
            var hashes = new byte[sorted.Count * 32];

            for (int i = 0; i < sorted.Count; i++)
            {
                var path = sorted[i];
                var content = provider.ReadFileBinary(path);
                var fileHash = HashFile(path, content, sha);
                Array.Copy(fileHash, 0, hashes, i * 32, 32);
            }

            var final = sha.ComputeHash(hashes);
            return BytesToHex(final);
        }

        private static string ComputeInstallSha256FromFiles(
            string folderPath, List<string> files, List<string> ignorePatterns)
        {
            var sorted = files
                .Select(f => f.Replace('\\', '/'))
                .Select(f => new
                {
                    FullPath = f,
                    RelativePath = GetRelativePath(folderPath, f)
                })
                .Where(x => !ShouldIgnore(x.RelativePath, ignorePatterns))
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using var sha = SHA256.Create();
            var hashes = new byte[sorted.Count * 32];

            for (int i = 0; i < sorted.Count; i++)
            {
                var content = File.ReadAllBytes(sorted[i].FullPath);
                var fileHash = HashFile(sorted[i].RelativePath, content, sha);
                Array.Copy(fileHash, 0, hashes, i * 32, 32);
            }

            var final = sha.ComputeHash(hashes);
            return BytesToHex(final);
        }

        private static byte[] HashFile(string relativePath, byte[] content, SHA256 sha)
        {
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            var combined = new byte[pathBytes.Length + content.Length];
            Array.Copy(pathBytes, 0, combined, 0, pathBytes.Length);
            Array.Copy(content, 0, combined, pathBytes.Length, content.Length);
            return sha.ComputeHash(combined);
        }

        private static bool ShouldIgnore(string relativePath, List<string> ignorePatterns)
        {
            var parts = relativePath.Split('/');
            foreach (var part in parts)
            {
                if (part.Length == 0)
                    continue;
                if (part.StartsWith("."))
                    return true;
            }

            if (ignorePatterns != null)
            {
                foreach (var pattern in ignorePatterns)
                {
                    if (MatchesWildcard(relativePath, pattern))
                        return true;
                }
            }

            return false;
        }

        private static bool MatchesWildcard(string path, string pattern)
        {
            return MatchesWildcardImpl(path, 0, pattern, 0);
        }

        private static bool MatchesWildcardImpl(string path, int pi, string pattern, int pj)
        {
            while (pi < path.Length && pj < pattern.Length && pattern[pj] != '*')
            {
                if (pattern[pj] != '?' && path[pi] != pattern[pj])
                    return false;
                pi++;
                pj++;
            }

            if (pj == pattern.Length)
                return pi == path.Length;

            if (pattern[pj] == '*')
            {
                pj++;
                if (pj == pattern.Length)
                    return true;

                while (pi < path.Length)
                {
                    if (MatchesWildcardImpl(path, pi, pattern, pj))
                        return true;
                    pi++;
                }
            }

            return false;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            var baseNormalized = basePath.Replace('\\', '/').TrimEnd('/') + "/";
            var fullNormalized = fullPath.Replace('\\', '/');
            if (fullNormalized.StartsWith(baseNormalized))
                return fullNormalized.Substring(baseNormalized.Length);
            return fullNormalized;
        }
    }
}
