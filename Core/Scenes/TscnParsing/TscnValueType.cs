namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Discriminator for <see cref="TscnPropertyValue"/>: is it a plain Godot Variant,
    /// an ExtResource reference (resolved at build time by SceneBuilder),
    /// or a SubResource reference (resolved from [sub_resource] definitions).
    /// </summary>
    public enum TscnValueType
    {
        Variant,
        ExtResourceRef,
        SubResourceRef
    }
}
