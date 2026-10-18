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
        /// <summary>Shipped with the game as a package in <c>collections/</c>.</summary>
        BuiltIn,

        /// <summary>Created, edited or downloaded by the player: a package in <c>user://collections/</c>.</summary>
        User,

        /// <summary>Fetched from an online catalog.</summary>
        Catalog,
    }
}
