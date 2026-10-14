using System;
using System.Text.RegularExpressions;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Parsed dependency from manifest.json "depends".
    /// String is parsed as: [modId][op][version]["?"]
    /// Where op is one of: ">=", "<=", ">", "<", "=", "^", "" (any version).
    /// "?" at the end — optional dependency: if mod is missing — not an error,
    /// but if present — version is checked.
    ///
    /// Version comparison — per-segment (paddedCompare). Segments may
    /// contain letters (1a, alpha-3, 1b.2.0). Each segment: if both
    /// parse as int — numeric comparison, otherwise string.CompareOrdinal.
    /// Both arrays are padded with "0" to equal length before comparison.
    /// </summary>
    public readonly struct ModDependency
    {
        private static readonly Regex ParseRegex = new(
            @"^([a-zA-Z_][a-zA-Z0-9_]*)(>=|<=|>|<|=|\^)?([a-zA-Z0-9][a-zA-Z0-9-]*(?:\.[a-zA-Z0-9-]+)*)?(\?)?$",
            RegexOptions.Compiled);

        /// <summary>Dependency mod id (the part before any operator).</summary>
        public string ModId { get; init; }

        /// <summary>Version operator: <c>">="</c>, <c>"&lt;="</c>, <c>"&gt;"</c>, <c>"&lt;"</c>, <c>"="</c>, <c>"^"</c>, or empty for "any version".</summary>
        public string Operator { get; init; }

        /// <summary>Raw version string as written in the manifest (e.g. <c>"1.0"</c>), or null when unconstrained.</summary>
        public string VersionRaw { get; init; }

        /// <summary>Version split on <c>.</c>; null when no version was given.</summary>
        public string[] VersionSegments { get; }

        /// <summary>True for a <c>modId[op][version]?</c> dependency — missing mod is tolerated, a present one must still satisfy the pin.</summary>
        public bool IsOptional { get; init; }

        /// <summary>True when the string parsed into a well-formed dependency.</summary>
        public bool IsValid { get; }

        /// <summary>Parse failure reason; meaningful only when <see cref="IsValid"/> is false.</summary>
        public string Error { get; }

        /// <summary>Creates the invalid default instance (see <see cref="Error"/>).</summary>
        public ModDependency()
        {
            ModId = string.Empty;
            Operator = string.Empty;
            VersionRaw = null;
            VersionSegments = null;
            IsOptional = false;
            IsValid = false;
            Error = "Default-constructed ModDependency is not valid";
        }

        private ModDependency(
            string modId,
            string op,
            string versionRaw,
            string[] segments,
            bool isOptional,
            bool isValid,
            string error)
        {
            ModId = modId;
            Operator = op;
            VersionRaw = versionRaw;
            VersionSegments = segments;
            IsOptional = isOptional;
            IsValid = isValid;
            Error = error;
        }

        /// <summary>
        /// Parses a manifest dependency string of the form
        /// <c>modId[op][version][?]</c>. Returns an invalid dependency (with
        /// <see cref="Error"/> set) for null, empty or malformed input instead
        /// of throwing, so one bad line cannot break manifest loading.
        /// </summary>
        public static ModDependency Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ModDependency(
                    string.Empty, string.Empty, null, null, false, false,
                    "Dependency string is null or empty");

            var trimmed = raw.Trim();

            var match = ParseRegex.Match(trimmed);
            if (!match.Success)
            {
                return new ModDependency(
                    string.Empty, string.Empty, null, null, false, false,
                    $"Invalid dependency format: '{trimmed}'");
            }

            var modId = match.Groups[1].Value;
            var op = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            var versionRaw = match.Groups[3].Success ? match.Groups[3].Value : null;
            var isOptional = match.Groups[4].Success;

            string[] segments = null;
            if (versionRaw != null)
            {
                segments = versionRaw.Split('.');
            }

            return new ModDependency(modId, op, versionRaw, segments, isOptional, true, null);
        }

        /// <summary>
        /// Checks the given installed version against this dependency's pin.
        /// Comparison is per-segment with zero-padding; <c>=</c> compares every
        /// segment (padded with "0") and <c>^</c> compares only the pinned
        /// prefix. An empty operator accepts any version.
        /// </summary>
        public bool IsSatisfiedBy(string actualVersion)
        {
            if (!IsValid)
                return false;

            if (string.IsNullOrEmpty(Operator))
                return true;

            var actualSegments = ParseSegments(actualVersion);
            var requiredSegments = VersionSegments;

            if (requiredSegments == null || requiredSegments.Length == 0)
                return Operator == "";

            switch (Operator)
            {
                case ">=":
                    return VersionHelper.CompareSegments(actualSegments, requiredSegments) >= 0;
                case "<=":
                    return VersionHelper.CompareSegments(actualSegments, requiredSegments) <= 0;
                case ">":
                    return VersionHelper.CompareSegments(actualSegments, requiredSegments) > 0;
                case "<":
                    return VersionHelper.CompareSegments(actualSegments, requiredSegments) < 0;
                case "=":
                    return ExactMatch(actualSegments, requiredSegments);
                case "^":
                    return PrefixMatch(actualSegments, requiredSegments);
                default:
                    return false;
            }
        }

        private static string[] ParseSegments(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new[] { "0" };

            return version.Trim().Split('.');
        }

        private static bool ExactMatch(string[] actual, string[] required)
        {
            var maxLen = Math.Max(actual.Length, required.Length);
            for (int i = 0; i < maxLen; i++)
            {
                var a = i < actual.Length ? actual[i] : "0";
                var r = i < required.Length ? required[i] : "0";
                if (!string.Equals(a, r, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool PrefixMatch(string[] actual, string[] required)
        {
            for (int i = 0; i < required.Length; i++)
            {
                var a = i < actual.Length ? actual[i] : "0";
                if (!string.Equals(a, required[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>Restores the canonical <c>modId[op][version][?]</c> string; empty when invalid.</summary>
        public override string ToString()
        {
            if (!IsValid)
                return string.Empty;

            var result = ModId;
            if (!string.IsNullOrEmpty(Operator) && VersionRaw != null)
                result += Operator + VersionRaw;
            if (IsOptional)
                result += "?";
            return result;
        }
    }
}
