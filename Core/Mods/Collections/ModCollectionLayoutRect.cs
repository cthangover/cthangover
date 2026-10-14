namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Position and size of one scheme group in canvas coordinates. Persisted by
    /// <see cref="CollectionLayoutStore"/> so the arrangement the player built
    /// survives restarts; canvas coordinates are independent of the viewport, so
    /// the same layout stays valid on any window size.
    /// <para/>
    /// <see cref="X"/> and <see cref="Y"/> stay <c>null</c> until the group is
    /// placed: the store serializes them away, so a layout saved by an older
    /// session (or a group added later) gets a fresh packed position instead of
    /// stacking at the origin.
    /// </summary>
    public class ModCollectionLayoutRect
    {
        /// <summary>Left edge; null until the player first places the group.</summary>
        public float? X { get; set; }

        /// <summary>Top edge; null until the player first places the group.</summary>
        public float? Y { get; set; }

        /// <summary>Frame width in canvas pixels.</summary>
        public float W { get; set; }

        /// <summary>Frame height in canvas pixels.</summary>
        public float H { get; set; }
    }
}
