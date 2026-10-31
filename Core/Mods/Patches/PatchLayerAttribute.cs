namespace Cthangover.Core.Mods.Patches
{
    using System;

    /// <summary>
    /// Marks a property whose JSON is a stored <b>layer</b> — a partial <see cref="Target"/> that is
    /// applied over another value later (an object's variant, a placement's per-instance fields, a
    /// state's components) — instead of a plain value. A patch or an <c>Extends</c> child that writes
    /// into a layer must not execute its operations on the layer itself: <c>Parts$add</c> into a
    /// variant without <c>Parts</c> would create a list that later <i>replaces</i> the object's parts.
    /// <see cref="JsonPatchMerger"/> therefore composes such patches with the layer (patch over patch),
    /// so that applying the composed layer equals applying the old layer and then the patch.
    /// <para>Three placements of the attribute:</para>
    /// <list type="bullet">
    /// <item>on a property holding one layer object;</item>
    /// <item>on a <c>string → layer</c> dictionary (<c>Variants</c>): every value is a layer;</item>
    /// <item>on the <c>[JsonExtensionData]</c> property: every property of the owner that is not a
    /// declared one is a layer entry (<c>RpgPlacement.Overrides</c>).</item>
    /// </list>
    /// <see cref="Wholesale"/> layers (state components) replace each named value whole when used, so
    /// a patch changes a value the layer names like plain data and refuses to change one it does not name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PatchLayerAttribute : Attribute
    {
        /// <summary>Declares a layer over <paramref name="target"/> (it supplies merge keys inside the layer).</summary>
        public PatchLayerAttribute(Type target)
        {
            Target = target;
        }

        /// <summary>The DTO the layer is applied to; merge keys and nested types inside the layer come from it.</summary>
        public Type Target { get; }

        /// <summary>
        /// True when every value of the layer replaces the target's value wholesale instead of merging
        /// into it. A patch may then change a value the layer already names (as ordinary data) or give a
        /// whole new value, but an operation on a value the layer does not name (<c>Interact.Commands$add</c>
        /// on a state without <c>Interact</c>) has no meaning and is a <see cref="JsonPatchException"/>.
        /// </summary>
        public bool Wholesale { get; set; }
    }
}
