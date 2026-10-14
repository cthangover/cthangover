namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One mod entry inside a resolved <see cref="ModCollectionGroup"/>. The
    /// group owns the presentation (frame, name, description) while this points
    /// at the actual mod and the leaf mechanic that declared it, so the scheme
    /// editor can show the mechanic role on the chip and remove the mod from
    /// the right <c>mechanics[].mods</c> array without walking the tree again.
    /// </summary>
    public class ModCollectionModRef
    {
        /// <summary>Canonical mod id (matches <see cref="ModInfo.Id"/>).</summary>
        public string ModId { get; set; }

        /// <summary>Leaf mechanic role the mod was declared with ("characters", "story", ...).</summary>
        public string Role { get; set; }

        /// <summary>Collection that owns the mechanic (may be nested inside the group's collection).</summary>
        public string CollectionId { get; set; }

        /// <summary>Owning mechanic id inside <see cref="CollectionId"/>.</summary>
        public string MechanicId { get; set; }
    }
}
