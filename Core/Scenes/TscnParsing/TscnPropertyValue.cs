namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>Wraps ExtResource("id") and SubResource("id") references. Resolved at build time by <see cref="TscnBuild.SceneBuilder"/>.</summary>
    public class TscnPropertyValue
    {
        /// <summary>Which kind of reference this value wraps.</summary>
        public TscnValueType Type { get; set; }
        /// <summary>Pre-converted Godot value when <see cref="TscnValueType.Variant"/>; default otherwise.</summary>
        public Godot.Variant Variant { get; set; }
        /// <summary>Local resource id for ExtResource/SubResource references.</summary>
        public string RefId { get; set; }

        /// <summary>True for <c>ExtResource("id")</c> values.</summary>
        public bool IsExtResourceRef => Type == TscnValueType.ExtResourceRef;
        /// <summary>True for <c>SubResource("id")</c> values.</summary>
        public bool IsSubResourceRef => Type == TscnValueType.SubResourceRef;

        /// <summary>Wraps an already-converted Variant value.</summary>
        public static TscnPropertyValue FromVariant(Godot.Variant v) => new() { Type = TscnValueType.Variant, Variant = v };
        /// <summary>Wraps an <c>ExtResource("id")</c> reference for later build-time resolution.</summary>
        public static TscnPropertyValue FromExtResource(string id) => new() { Type = TscnValueType.ExtResourceRef, RefId = id };
        /// <summary>Wraps a <c>SubResource("id")</c> reference for later build-time resolution.</summary>
        public static TscnPropertyValue FromSubResource(string id) => new() { Type = TscnValueType.SubResourceRef, RefId = id };
    }
}
