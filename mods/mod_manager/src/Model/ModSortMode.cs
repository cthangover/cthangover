namespace Mods.ModManager
{
    /// <summary>Ordering mode of the mod grid; grouping by category is a separate toggle.</summary>
    public enum ModSortMode
    {
        /// <summary>Alphabetical by display title.</summary>
        Name,

        /// <summary>Alphabetical by author, then title.</summary>
        Author,

        /// <summary>By install/update state (installed, updates, catalog-only).</summary>
        Status,
    }
}
