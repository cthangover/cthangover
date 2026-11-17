namespace Cthangover.Core.Mods.Resolvers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable output of one provider/include read. Runtime consumes ExpandedText; editors save RawText.
    /// Origins describe the same emitted bytes, and source identity includes every consumed resource and provider instance.
    /// </summary>
    public sealed class ResolvedModText
    {
        private readonly IModFileProvider provider;
        private readonly string inputSignature;
        private readonly int[] lineStarts;
        private readonly Dictionary<int, int> rootDirectives = new();

        internal ResolvedModText(IModFileProvider provider, string path, string rawText, string expandedText,
            ModTextOrigin[] origins, int[] rootIncludeLines, KeyValuePair<string, string>[] inputSnapshots, string inputSignature)
        {
            this.provider = provider;
            this.inputSignature = inputSignature;
            Path = path;
            RawText = rawText;
            ExpandedText = expandedText;
            Origins = Array.AsReadOnly(origins);
            RootIncludeLines = Array.AsReadOnly(rootIncludeLines);
            InputSnapshots = Array.AsReadOnly(inputSnapshots);
            foreach (var origin in origins)
            {
                var meaningful = false;
                for (var i = origin.Start; i < origin.Start + origin.Length; i++)
                {
                    if (!char.IsWhiteSpace(expandedText[i]))
                    {
                        meaningful = true;
                        break;
                    }
                }

                if (meaningful)
                {
                    if (rootDirectives.TryGetValue(origin.RootLine, out var directive) && directive != origin.RootIncludeOffset)
                    {
                        rootDirectives[origin.RootLine] = -1;
                    }
                    else if (!rootDirectives.ContainsKey(origin.RootLine))
                    {
                        rootDirectives.Add(origin.RootLine, origin.RootIncludeOffset);
                    }
                }
            }

            var starts = new List<int> { 0 };
            for (var i = 0; i < expandedText.Length; i++)
            {
                if (expandedText[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            lineStarts = starts.ToArray();
        }

        /// <summary>Canonical logical root path used for this read.</summary>
        public string Path { get; }

        /// <summary>Exact root provider text, including its original header, directives and line endings.</summary>
        public string RawText { get; }

        /// <summary>Exact program after the shared include engine; runtime line coordinates refer to this string.</summary>
        public string ExpandedText { get; }

        /// <summary>Read-only emitted spans, including composite lines whose navigation cannot be projected safely.</summary>
        public IReadOnlyList<ModTextOrigin> Origins { get; }

        /// <summary>Authored include rows in encounter order, including empty fragments that emit no action or text.</summary>
        public IReadOnlyList<int> RootIncludeLines { get; }

        /// <summary>
        /// Every successful provider read in consumption order, including the raw root, empty fragments and repeated paths.
        /// Editors validate duplicate-path consistency and planned destination resolution before copying any raw dependency.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> InputSnapshots { get; }

        /// <summary>
        /// Refuses empty rows and mixed direct/include or multiple-directive rows rather than choosing an arbitrary target.
        /// </summary>
        public bool CanProjectRootLine(int rootLine)
        {
            return rootDirectives.TryGetValue(rootLine, out var directive) && directive >= 0;
        }

        /// <summary>
        /// Returns provenance only when all non-whitespace characters of the expanded row agree on one root/directive.
        /// Blank, out-of-range and mixed-origin inline rows return null instead of approximate coordinates.
        /// </summary>
        public ModTextOrigin OriginOfLine(int expandedLine)
        {
            if (expandedLine < 1 || expandedLine > lineStarts.Length)
            {
                return null;
            }

            var start = lineStarts[expandedLine - 1];
            var end = expandedLine < lineStarts.Length ? lineStarts[expandedLine] : ExpandedText.Length;
            ModTextOrigin found = null;
            foreach (var origin in Origins)
            {
                if (origin.Start >= end)
                {
                    break;
                }

                var left = Math.Max(start, origin.Start);
                var right = Math.Min(end, origin.Start + origin.Length);
                var meaningful = false;
                for (var i = left; i < right; i++)
                {
                    if (!char.IsWhiteSpace(ExpandedText[i]))
                    {
                        meaningful = true;
                        break;
                    }
                }

                if (!meaningful)
                {
                    continue;
                }

                if (found != null && (found.RootLine != origin.RootLine || found.RootIncludeOffset != origin.RootIncludeOffset))
                {
                    return null;
                }

                found ??= origin;
            }

            return found;
        }

        /// <summary>Detects provider replacement and any consumed source change, even when emitted text remains identical.</summary>
        public bool HasSameSource(ResolvedModText other)
        {
            return other != null && ReferenceEquals(provider, other.provider) && Path == other.Path
                && inputSignature == other.inputSignature && RawText == other.RawText && ExpandedText == other.ExpandedText;
        }
    }
}
