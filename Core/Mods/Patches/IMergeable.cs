namespace Cthangover.Core.Mods.Patches
{
    /// <summary>
    /// Marks an element type as having an identity for array patch operations:
    /// <c>$add</c> deep-merges an element into the existing one with the same
    /// <see cref="MergeKeyProperty"/> value and <c>$remove</c> removes by it, in
    /// every list of this element type at any depth. A key declared on the
    /// property itself (<see cref="MergeKeyAttribute"/>) takes precedence. Implement
    /// it explicitly so the key does not get serialised with the DTO.
    /// </summary>
    public interface IMergeable
    {
        /// <summary>
        /// The name of the property used as the identity key for
        /// element matching during array merge operations.
        /// </summary>
        string MergeKeyProperty { get; }
    }
}
