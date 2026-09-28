namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Where a <see cref="ModCollection"/> was loaded from. Determines merge
    /// precedence in <see cref="ModCollectionStore.LoadAll"/>: player-created
    /// collections override built-in ones, built-in ones override catalog ones,
    /// so a shipping mod can always fix or extend an online collection.
    /// </summary>
    public enum ModCollectionOrigin
    {
        /// <summary>Shipped inside a mod folder (<c>collections/*.json</c>).</summary>
        BuiltIn,

        /// <summary>Created or edited by the player in <c>user://collections.json</c>.</summary>
        User,

        /// <summary>Fetched from an online catalog.</summary>
        Catalog,
    }
}
