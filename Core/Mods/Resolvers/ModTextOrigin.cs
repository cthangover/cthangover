namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>A contiguous emitted span retains its provider path/line and the authored root directive that owns it.</summary>
    public sealed class ModTextOrigin
    {
        internal ModTextOrigin(int start, int length, string path, int line, int rootLine, int rootIncludeOffset)
        {
            Start = start;
            Length = length;
            Path = path;
            Line = line;
            RootLine = rootLine;
            RootIncludeOffset = rootIncludeOffset;
        }

        /// <summary>Zero-based character offset in the exact expanded text.</summary>
        public int Start { get; }

        /// <summary>Number of emitted characters, without crossing a source newline.</summary>
        public int Length { get; }

        /// <summary>Canonical logical path inside the selected provider, never a filesystem path.</summary>
        public string Path { get; }

        /// <summary>One-based line in that provider resource before include trimming.</summary>
        public int Line { get; }

        /// <summary>One-based authored root line, or the root directive's line for nested resources.</summary>
        public int RootLine { get; }

        /// <summary>One-based root include token offset distinguishes directives sharing a row; zero means direct root text.</summary>
        public int RootIncludeOffset { get; }
    }
}
