using System;
using System.Collections.Concurrent;
using System.IO;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Single case policy for mod-relative paths: a reference must match the
    /// on-disk (or in-archive) spelling exactly. Windows and macOS file
    /// systems forgive a wrong case, Linux, CI and exported <c>.pck</c> files
    /// do not — so a mod that only works on the author's machine is caught on
    /// every platform instead of breaking after export. Providers use
    /// <see cref="FindOnDisk"/> to learn the real spelling and
    /// <see cref="ReportMismatch"/> to name the correct path in the log, then
    /// treat the request as a miss.
    /// </summary>
    public static class ModPathCase
    {
        private static readonly ConcurrentDictionary<string, byte> Reported = new(StringComparer.Ordinal);

        /// <summary>
        /// Walks <paramref name="relative"/> (canonical <see cref="ModPath"/>
        /// form) under <paramref name="root"/> segment by segment and returns
        /// its on-disk spelling, preferring an exact match per segment and
        /// falling back to a case-insensitive one. Returns <c>null</c> when
        /// nothing matches or the entry kind (file vs directory) differs.
        /// </summary>
        public static string FindOnDisk(string root, string relative, bool directory)
        {
            if (string.IsNullOrEmpty(relative))
                return directory && Directory.Exists(root) ? string.Empty : null;

            var segments = relative.Split('/');
            var actual = new string[segments.Length];
            var current = root;

            for (var i = 0; i < segments.Length; i++)
            {
                if (!Directory.Exists(current))
                    return null;

                string match = null;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    var name = Path.GetFileName(entry);
                    if (string.Equals(name, segments[i], StringComparison.Ordinal))
                    {
                        match = name;
                        break;
                    }

                    if (match == null && string.Equals(name, segments[i], StringComparison.OrdinalIgnoreCase))
                        match = name;
                }

                if (match == null)
                    return null;

                actual[i] = match;
                current = Path.Combine(current, match);
            }

            var exists = directory ? Directory.Exists(current) : File.Exists(current);
            return exists ? string.Join("/", actual) : null;
        }

        /// <summary>
        /// Logs a case-only mismatch once per mod and path, so a hot lookup
        /// loop cannot flood the log while the author still sees the fix.
        /// </summary>
        public static void ReportMismatch(string mod, string requested, string actual)
        {
            if (!Reported.TryAdd($"{mod}|{requested}", 0))
                return;

            GameLogger.Log("MODS",
                $"[{mod}] path case mismatch: '{requested}' is spelled '{actual}' — fix the reference " +
                "(paths are case-sensitive on Linux and in exported .pck)",
                LogLevel.Error);
        }
    }
}
