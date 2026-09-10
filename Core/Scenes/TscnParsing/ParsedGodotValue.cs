namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>Lightweight wrapper for Godot-specific property values (Vector2, Color, NodePath, etc.) — avoids creating actual Godot objects during parsing to remain testable outside the engine.</summary>
    public class ParsedGodotValue
    {
        public string TypeName { get; set; }
        public System.Collections.Generic.List<object> Args { get; set; } = new();

        public static ParsedGodotValue Create(string typeName, params object[] args) => new()
        {
            TypeName = typeName,
            Args = new System.Collections.Generic.List<object>(args)
        };

        public object Arg(int index) => index < Args.Count ? Args[index] : null;
    }
}
