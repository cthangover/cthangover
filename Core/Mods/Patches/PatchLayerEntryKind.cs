namespace Cthangover.Core.Mods.Patches
{
    /// <summary>The forms of a <see cref="PatchLayerEntry"/>.</summary>
    internal enum PatchLayerEntryKind
    {
        /// <summary>The layer does not touch the property.</summary>
        None,

        /// <summary>The property gets a value that does not depend on the base (or is removed).</summary>
        Concrete,

        /// <summary>An object is merged into the property.</summary>
        Fragment,

        /// <summary><c>$remove</c> then <c>$add</c> on the property's list.</summary>
        Operations,
    }
}
