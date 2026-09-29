namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Case-sensitive glob matcher used by scene-patch masks (<c>_patch_delete_nodes</c>,
    /// <c>_patch_remove</c>). <c>*</c> matches any run of characters including <c>/</c> (so a
    /// node mask can span hierarchy levels), <c>?</c> matches exactly one character.
    /// The engine has no general-purpose wildcard helper, and this is deliberately tiny:
    /// an iterative two-pointer matcher with backtracking avoids regex compilation per key
    /// on the scene-load hot path.
    /// </summary>
    public static class MaskMatcher
    {
        /// <summary>
        /// True when <paramref name="value"/> matches the glob <paramref name="pattern"/>.
        /// Null/empty patterns never match; a pattern without wildcards is an exact comparison.
        /// </summary>
        public static bool IsMatch(string pattern, string value)
        {
            if (string.IsNullOrEmpty(pattern) || value == null)
                return false;

            var p = 0;
            var v = 0;
            var star = -1;
            var mark = 0;

            while (v < value.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == value[v]))
                {
                    p++;
                    v++;
                    continue;
                }

                if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p;
                    mark = v;
                    p++;
                    continue;
                }

                if (star >= 0)
                {
                    p = star + 1;
                    v = ++mark;
                    continue;
                }

                return false;
            }

            while (p < pattern.Length && pattern[p] == '*')
                p++;

            return p == pattern.Length;
        }

        /// <summary>True when the text contains at least one wildcard character.</summary>
        public static bool HasMask(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '*' || text[i] == '?')
                    return true;
            }

            return false;
        }
    }
}
