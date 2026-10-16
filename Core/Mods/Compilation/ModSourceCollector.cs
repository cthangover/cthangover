using System;
using System.Collections.Generic;
using System.Threading;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Source-collection step of the mod compilation pipeline. Expands the
    /// source patterns declared in a mod's <c>manifest.json</c> into a flat
    /// list of <c>(path, text)</c> pairs by walking the mod's
    /// <see cref="IModFileProvider"/>, so folder mods and zip mods are
    /// collected through the same code path.
    ///
    /// A pattern splits at its last <c>/</c> into a directory prefix and a
    /// suffix filter (<c>*.cs</c> by default); matching is suffix-based
    /// rather than glob-based because providers list entries as
    /// slash-separated relative paths. When the declared patterns match
    /// nothing, the whole mod root is scanned as a fallback so a mod that
    /// just drops <c>.cs</c> files anywhere still compiles.
    /// </summary>
    public static class ModSourceCollector
    {
        /// <summary>
        /// Collects all source files of a mod: every pattern from
        /// <c>Manifest.Sources</c> is expanded in declaration order, each
        /// producing (provider path, file text) pairs. If nothing matched,
        /// the full-mod fallback scan runs, so the result is empty only for
        /// a mod that truly has no C# sources. Honors
        /// <paramref name="cancellationToken"/> between provider calls to
        /// keep boot cancellation responsive.
        /// </summary>
        public static List<(string FilePath, string Source)> CollectSourceFiles(ModInfo modInfo, CancellationToken cancellationToken)
        {
            var result = new List<(string, string)>();

            foreach (var pattern in modInfo.Manifest.Sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var prefix = pattern;
                var suffix = "*.cs";
                var sepIndex = pattern.LastIndexOf('/');
                if (sepIndex >= 0)
                {
                    prefix = pattern.Substring(0, sepIndex);
                    suffix = pattern.Substring(sepIndex + 1);
                }

                CollectSourceFilesRecursive(modInfo.FileProvider, prefix, file =>
                {
                    if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var fileName = file.Replace('\\', '/');
                    var fileNamePart = fileName.Substring(fileName.LastIndexOf('/') + 1);

                    return suffix == "*" || suffix == "*.cs" ||
                        (suffix.StartsWith("*.") && fileNamePart.EndsWith(suffix.Substring(1), StringComparison.OrdinalIgnoreCase));
                }, result, cancellationToken);
            }

            if (result.Count == 0)
            {
                CollectSourceFilesRecursive(modInfo.FileProvider, "", file =>
                {
                    return file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                }, result, cancellationToken);
            }

            return result;
        }

        private static void CollectSourceFilesRecursive(
            IModFileProvider provider,
            string directory,
            Func<string, bool> fileFilter,
            List<(string, string)> result,
            CancellationToken cancellationToken)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectSourceFilesRecursive(provider, subDir, fileFilter, result, cancellationToken);
                }
                else if (fileFilter(entry))
                {
                    var source = provider.ReadFileText(entry);
                    result.Add((entry, source));
                }
            }
        }
    }
}
