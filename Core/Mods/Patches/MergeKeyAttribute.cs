namespace Cthangover.Core.Mods.Patches
{
    using System;

    /// <summary>
    /// Declares the identity of the elements of a list property for patch operations:
    /// <c>Parts$add</c> with an element whose key matches an existing one deep-merges
    /// into it instead of appending, and <c>Parts$remove</c> removes by key. Several
    /// names form a composite key (all must be equal). The patch engine never guesses a
    /// key from property names; besides this attribute only <see cref="IMergeable"/> on
    /// the element type and the explicit patch form <c>{"$key": …, "$items": […]}</c>
    /// give one (see <see cref="JsonPatchMerger"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MergeKeyAttribute : Attribute
    {
        /// <summary>Declares the element property (or properties) that identify an element.</summary>
        public MergeKeyAttribute(params string[] properties)
        {
            Properties = properties ?? Array.Empty<string>();
        }

        /// <summary>Element property names forming the key, compared as JSON names (case-insensitive).</summary>
        public string[] Properties { get; }
    }
}
