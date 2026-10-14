namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Position of one mod chip inside its group, relative to the group's mods
    /// layer. Persisted by <see cref="CollectionLayoutStore"/> next to
    /// <see cref="ModCollectionLayoutRect"/> so the player's manual arrangement
    /// of chips is restored together with the group frames.
    /// </summary>
    public class ModCollectionLayoutPoint
    {
        /// <summary>Horizontal offset inside the group, in canvas pixels.</summary>
        public float X { get; set; }

        /// <summary>Vertical offset inside the group, in canvas pixels.</summary>
        public float Y { get; set; }
    }
}
