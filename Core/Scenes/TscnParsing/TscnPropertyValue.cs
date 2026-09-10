namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>Wraps ExtResource("id") and SubResource("id") references. Resolved at build time by <see cref="TscnBuild.SceneBuilder"/>.</summary>
    public class TscnPropertyValue
    {
        public TscnValueType Type { get; set; }
        public Godot.Variant Variant { get; set; }
        public string RefId { get; set; }

        public bool IsExtResourceRef => Type == TscnValueType.ExtResourceRef;
        public bool IsSubResourceRef => Type == TscnValueType.SubResourceRef;

        public static TscnPropertyValue FromVariant(Godot.Variant v) => new() { Type = TscnValueType.Variant, Variant = v };
        public static TscnPropertyValue FromExtResource(string id) => new() { Type = TscnValueType.ExtResourceRef, RefId = id };
        public static TscnPropertyValue FromSubResource(string id) => new() { Type = TscnValueType.SubResourceRef, RefId = id };
    }
}
