using System;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Per-segment version comparison. Version is split on '.',
    /// segments may contain letters ("1a", "alpha-3", "1b.2.0").
    ///
    /// Algorithm: both arrays are padded with "0" to equal length, then
    /// lexicographic comparison. Each segment: if both parse as
    /// int — numeric comparison, otherwise string.CompareOrdinal.
    ///
    /// This is the only version comparison algorithm in the project —
    /// used in mod deduplication (ModRegistry.RegisterCandidates)
    /// and in versioned depends (ModDependency.IsSatisfiedBy).
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// Compares two version strings. Negative if a &lt; b,
        /// zero if equal, positive if a &gt; b.
        /// </summary>
        public static int Compare(string a, string b)
        {
            a = a?.Trim() ?? "0.0.0.0";
            b = b?.Trim() ?? "0.0.0.0";

            if (a.Length == 0) a = "0.0.0.0";
            if (b.Length == 0) b = "0.0.0.0";

            if (a == b)
                return 0;

            var segA = a.Split('.');
            var segB = b.Split('.');

            return CompareSegments(segA, segB);
        }

        /// <summary>
        /// Per-segment array comparison. Both are padded with "0" to
        /// equal length. Each segment: int comparison when
        /// possible, otherwise Ordinal.
        /// </summary>
        public static int CompareSegments(string[] a, string[] b)
        {
            var maxLen = Math.Max(a.Length, b.Length);
            for (int i = 0; i < maxLen; i++)
            {
                var sa = i < a.Length ? a[i] : "0";
                var sb = i < b.Length ? b[i] : "0";

                int cmp;
                if (int.TryParse(sa, out var ia) && int.TryParse(sb, out var ib))
                    cmp = ia.CompareTo(ib);
                else
                    cmp = string.CompareOrdinal(sa, sb);

                if (cmp != 0)
                    return cmp;
            }
            return 0;
        }

        /// <summary>
        /// Returns true when <c>a &lt; b</c> (a is an older version).
        /// </summary>
        public static bool IsLessThan(string a, string b)
        {
            return Compare(a, b) < 0;
        }

        /// <summary>
        /// Returns true when <c>a &gt; b</c> (a is a newer version).
        /// </summary>
        public static bool IsGreaterThan(string a, string b)
        {
            return Compare(a, b) > 0;
        }
    }
}
