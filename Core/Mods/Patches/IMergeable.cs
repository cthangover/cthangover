namespace Cthangover.Core.Mods.Patches
{
    /// <summary>
    /// Marks a class as supporting identity-key-based array merge.
    /// When a patch uses <c>$add</c> or <c>$remove</c> on a registered
    /// array property, elements are matched by the value of
    /// <see cref="MergeKeyProperty"/> instead of raw JSON text
    /// comparison, enabling per-field property overwrite.
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
