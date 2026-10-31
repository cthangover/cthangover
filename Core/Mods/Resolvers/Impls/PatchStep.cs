namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>Outcome of applying one property patch (<see cref="PatchResolver.ApplyOne"/>).</summary>
    public enum PatchStep
    {
        /// <summary>The patch merged and the result reads back as the DTO.</summary>
        Applied,

        /// <summary>The patch could not apply (wrong operation, wrong type); the entity is unchanged.</summary>
        Skipped,

        /// <summary>The patch says <c>"$delete": true</c>: the entity is removed.</summary>
        Deleted,
    }
}
