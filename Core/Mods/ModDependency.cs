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

        public string ModId { get; init; }
        public string Operator { get; init; }
        public string VersionRaw { get; init; }
        public string[] VersionSegments { get; }
        public bool IsOptional { get; init; }

        public bool IsValid { get; }
        public string Error { get; }

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
