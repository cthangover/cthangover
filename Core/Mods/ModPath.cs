using System;
using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Canonical path arithmetic shared by every <see cref="IModFileProvider"/>.
    /// Mod-relative paths are always stored and compared in one canonical form:
    /// forward slashes, no leading root marker, no <c>.</c>/<c>..</c> segments,
    /// no trailing slash. Only providers convert to the OS-native form, and only
    /// at the filesystem boundary.
    ///
    /// The previous per-provider normalisation diverged — folder providers
    /// converted to <see cref="System.IO.Path.DirectorySeparatorChar"/> while zip
    /// providers kept forward slashes and trimmed leading slashes — so the same
    /// mod could behave differently depending on whether it shipped as a folder
    /// or a zip (most visibly: <c>${../shared/x.json}</c> includes resolved
    /// against the real filesystem for folders, but failed for zips because the
    /// <c>..</c> was never collapsed). Centralising here removes that class of
    /// bug: a path produced by one provider can be handed to any other component
    /// without re-normalisation.
    ///
    /// Escaping the mod root is neutralised rather than rejected: a leading
    /// <c>/</c>, a drive prefix (<c>C:/</c>), or a <c>..</c> that would pop past
    /// the root simply becomes a relative path inside the mod, matching the
    /// fail-closed behaviour of zip lookup.
    /// </summary>
    public static class ModPath
    {
        /// <summary>
        /// Converts a path to canonical mod-relative form: backslashes become
        /// forward slashes, empty/current-directory segments are dropped,
        /// <c>..</c> pops the previous segment (ignored at the root), and a
        /// leading root or drive marker is removed. Returns an empty string for
        /// null, empty, or root-only input.
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            var segments = path.Replace('\\', '/').Split('/');
            var result = new List<string>(segments.Length);

            foreach (var segment in segments)
            {
                if (segment.Length == 0 || segment == ".")
                    continue;

                if (segment == "..")
                {
                    if (result.Count > 0)
                        result.RemoveAt(result.Count - 1);
                    continue;
                }

                if (result.Count == 0 && segment.EndsWith(":", StringComparison.Ordinal))
                    continue;

                result.Add(segment);
            }

            return string.Join("/", result);
        }

        /// <summary>
        /// Canonical directory form: <see cref="Normalize"/> plus a trailing
        /// <c>/</c>. Empty input stays empty so that root listings keep the
        /// no-prefix contract.
        /// </summary>
        public static string NormalizeDirectory(string path)
        {
            var normalized = Normalize(path);
            return normalized.Length == 0 ? "" : normalized + "/";
        }

        /// <summary>
        /// Returns the canonical directory containing <paramref name="path"/>,
        /// or an empty string when the path has no directory component.
        /// </summary>
        public static string GetDirectory(string path)
        {
            var normalized = Normalize(path);
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash < 0 ? "" : normalized.Substring(0, lastSlash);
        }

        /// <summary>
        /// Resolves a relative include path (<c>../shared/data.json</c>)
        /// against the directory of <paramref name="currentPath"/> and returns
        /// the canonical result. This is the single implementation behind
        /// <see cref="IModFileProvider.ResolveIncludePath"/> for both folder and
        /// zip providers, so include directives cannot behave differently based
        /// on storage format.
        /// </summary>
        public static string ResolveRelative(string includePath, string currentPath)
        {
            var directory = GetDirectory(currentPath);
            if (directory.Length == 0)
                return Normalize(includePath);

            return Normalize(directory + "/" + includePath);
        }
    }
}
