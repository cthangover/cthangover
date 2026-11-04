namespace Cthangover.TestRunner
{
    using System.Text.RegularExpressions;

    /// <summary>Ordinal glob matching for catalogue names; commas join alternatives without interpreting regex syntax.</summary>
    public static class NamePattern
    {
        public static bool Matches(string value, string pattern)
        {
            if (pattern == null) return true;
            return pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(p => Regex.IsMatch(value ?? "", "\\A" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "\\z", RegexOptions.CultureInvariant));
        }
    }
}
