namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>Lightweight wrapper for Godot-specific property values (Vector2, Color, NodePath, etc.) — avoids creating actual Godot objects during parsing to remain testable outside the engine.</summary>
    public class ParsedGodotValue
    {
        /// <summary>Constructor-style name of the Godot value, e.g. "Vector2", "Color", "NodePath".</summary>
        public string TypeName { get; set; }
        /// <summary>Positional arguments inside the parentheses; numbers are stored as float/int, strings as string.</summary>
        public System.Collections.Generic.List<object> Args { get; set; } = new();

        /// <summary>Convenience factory for parsers and tests.</summary>
        public static ParsedGodotValue Create(string typeName, params object[] args) => new()
        {
            TypeName = typeName,
            Args = new System.Collections.Generic.List<object>(args)
        };

        /// <summary>Returns the argument at <paramref name="index"/>, or null when out of range (Godot constructors allow omitted trailing args).</summary>
        public object Arg(int index) => index < Args.Count ? Args[index] : null;
    }
}
