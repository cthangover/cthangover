namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Resolves managed library declarations through the selected provider, including archives.
    /// The legacy dll folder remains implicit; explicit references are required, mod-relative globs.
    /// Compilation, staging and cache signatures use the same file set.
    /// </summary>
    public static class ModManagedReferences
    {
        /// <summary>
        /// Returns deterministic, deduplicated provider paths and owned byte arrays. Explicit patterns
        /// cannot escape the mod or silently match nothing; nested mods retain their separate ownership.
        /// </summary>
        public static List<(string FilePath, byte[] Bytes)> Collect(IModInfo mod)
        {
            var result = new List<(string FilePath, byte[] Bytes)>();
            if (mod?.FileProvider == null)
            {
                return result;
            }

            var files = new SortedSet<string>(StringComparer.Ordinal);
            var provider = NestedFolders.Visible(mod);
            CollectFiles(provider, "dll", files);
            var patterns = (mod as ModInfo)?.Manifest?.References;
            if (patterns != null)
            {
                var candidates = new SortedSet<string>(StringComparer.Ordinal);
                CollectFiles(provider, string.Empty, candidates);
                foreach (var pattern in patterns)
                {
                    var matcher = Pattern(pattern);
                    var matched = false;
                    foreach (var file in candidates)
                    {
                        if (matcher.IsMatch(file))
                        {
                            files.Add(file);
                            matched = true;
                        }
                    }

                    if (!matched)
                    {
                        throw new FileNotFoundException($"Mod '{mod.Id}': managed reference '{pattern}' matched no DLL");
                    }
                }
            }

            foreach (var file in files)
            {
                var bytes = provider.ReadFileBinary(file);
                if (bytes == null)
                {
                    throw new FileNotFoundException($"Mod '{mod.Id}': managed reference disappeared", file);
                }

                result.Add((file, bytes));
            }

            return result;
        }

        /// <summary>
        /// Includes the current owner and its dependency closure, so changed DLL bytes invalidate
        /// consumers even when sources, manifest versions and dependency assembly names stay unchanged.
        /// </summary>
        public static string BuildSignature(ModInfo owner, IDictionary<string, IModInfo> mods)
        {
            var ids = new SortedSet<string>(ModCompiler.DependencyClosure(owner, mods), StringComparer.Ordinal);
            ids.Add(owner.Id);
            var signature = new StringBuilder();
            foreach (var id in ids)
            {
                if (!mods.TryGetValue(id, out var mod))
                {
                    continue;
                }

                foreach (var file in Collect(mod))
                {
                    signature.Append(id).Append('/').Append(file.FilePath).Append(':');
                    signature.Append(Convert.ToHexString(SHA256.HashData(file.Bytes))).Append(';');
                }
            }

            return signature.ToString();
        }

        private static void CollectFiles(IModFileProvider provider, string directory, SortedSet<string> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/", StringComparison.Ordinal))
                {
                    CollectFiles(provider, entry.TrimEnd('/'), result);
                }
                else if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }
        }

        private static Regex Pattern(string pattern)
        {
            var normalized = pattern?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized)
                || normalized.Contains(':') || normalized.Contains(";")
                || normalized.Split('/').AsSpan().Contains("..")
                || !normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Managed reference must be a mod-relative DLL pattern: " + pattern);
            }

            var expression = Regex.Escape(ModPath.Normalize(normalized));
            expression = expression.Replace(@"\*\*/", "(?:.*/)?").Replace(@"\*\*", ".*");
            expression = expression.Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]");
            return new Regex("^" + expression + "$", RegexOptions.CultureInvariant);
        }
    }
}
