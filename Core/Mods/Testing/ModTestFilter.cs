namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Case-name globs used by --test-filter; commas join alternatives and regex metacharacters stay literal.</summary>
    public static class ModTestFilter
    {
        public static bool Matches(string name, string filter)
        {
            if (filter == null) return true;
            if (string.IsNullOrWhiteSpace(filter)) throw new ArgumentException("empty mod test filter");
            return filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(pattern => Regex.IsMatch(name, "\\A" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "\\z", RegexOptions.CultureInvariant));
        }
    }
}
