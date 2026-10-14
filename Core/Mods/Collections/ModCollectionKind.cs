namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Role a <see cref="ModCollection"/> plays in the catalog and in the scheme
    /// editor. The kind is metadata only — resolution does not depend on it —
    /// but it drives the game picker: only <see cref="Game"/> collections are
    /// offered as playable assemblies, while <see cref="Component"/> and
    /// <see cref="Toolset"/> blocks are building material for the scheme.
    /// Missing values deserialize to <see cref="Game"/> so collections written
    /// before kinds existed keep working as standalone games.
    /// </summary>
    public enum ModCollectionKind
    {
        /// <summary>Finished composition the player can assemble and play.</summary>
        Game,

        /// <summary>Reusable block: a mechanic, a system layer or content group.</summary>
        Component,

        /// <summary>Developer toolset (editors, validators, the mod manager).</summary>
        Toolset,
    }
}
