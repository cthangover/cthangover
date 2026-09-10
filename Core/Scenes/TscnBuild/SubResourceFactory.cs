using System;
using System.Collections.Generic;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Builds Godot Resources from parsed [sub_resource] definitions. Delegates type-specific construction
    /// to <see cref="SubResourceBuilderRegistry"/>. <see cref="ToVariant"/> is the single boundary where
    /// ParsedGodotValue wrappers become actual Godot native objects — only called inside Godot runtime.
    /// </summary>
    public class SubResourceFactory
    {
        private readonly Dictionary<string, Resource> _built = new();

        /// <summary>Register a pre-built resource under its sub_resource id. Used so that BuildAll can resolve cross-references between sub-resources.</summary>
        public void Register(string id, Resource r) { _built[id] = r; }
        /// <summary>Retrieves a previously-built resource by its sub_resource id. Returns null if not found.</summary>
        public Resource Get(string id) { _built.TryGetValue(id, out var r); return r; }

        /// <summary>Builds all sub-resources from the parsed scene. Uses a callback for cross-resource resolution (e.g. GradientTexture1D referencing a Gradient).</summary>
        public void BuildAll(List<SubResourceDef> subs, Func<string, Resource> resourceResolver)
        {
            foreach (var def in subs)
            {
                if (_built.ContainsKey(def.Id)) continue;
                var r = Build(def, resourceResolver);
                if (r != null) _built[def.Id] = r;
            }
        }

        private static Resource Build(SubResourceDef def, Func<string, Resource> resourceResolver)
        {
            var b = SubResourceBuilderRegistry.Get(def.Type) ?? SubResourceBuilderRegistry.Get(def.Type == "Font" ? "FontFile" : null);
            if (b == null) { GameLogger.Log("TSCN_BUILD", $"SubResourceFactory: unsupported type '{def.Type}'", LogLevel.Warning); return null; }
            return b.Build(def, new SubResourceBuildContext { ResourceResolver = resourceResolver });
        }

        internal static void ApplyProperties(GodotObject obj, SubResourceDef def) { foreach (var kvp in def.OrderedProperties) SetProperty(obj, kvp.Key, kvp.Value); }
        internal static void SetProperty(GodotObject obj, string key, object value) { if (value is TscnPropertyValue) return; obj.Set(key, ToVariant(value)); }

        /// <summary>Converts a parsed value to Godot Variant. This is the boundary where ParsedGodotValue→Godot struct conversion happens — only runs inside Godot runtime.</summary>
        internal static Variant ToVariant(object value)
        {
            if (value == null) return default;
            if (value is ParsedGodotValue pgv) return GodotValueToVariant(pgv);
            if (value is ParsedGodotValue[] pga) return GodotValueArrayToVariant(pga);
            switch (value) { case bool b: return b; case int i: return i; case long l: return l; case float f: return f; case double d: return (float)d; case string s: return s; case string[] sa: return CreateArray(sa); case int[] ia: return CreateArray(ia); case long[] la: return CreateArray(la); case float[] fa: return CreateArray(fa); case double[] da: return CreateArray(Array.ConvertAll(da, x => (float)x)); case byte[] ba: return CreateArray(ba); default: return default; }
        }

        private static Variant GodotValueToVariant(ParsedGodotValue pgv) { var a = pgv.Args; switch (pgv.TypeName) { case "NodePath": return a.Count > 0 && a[0] is string np ? new NodePath(np) : default; case "StringName": return a.Count > 0 && a[0] is string sn ? new StringName(sn) : default; case "Vector2": return new Vector2(AsF(a, 0), AsF(a, 1)); case "Vector2i": return new Vector2I(AsI(a, 0), AsI(a, 1)); case "Vector3": return new Vector3(AsF(a, 0), AsF(a, 1), AsF(a, 2)); case "Vector3i": return new Vector3I(AsI(a, 0), AsI(a, 1), AsI(a, 2)); case "Vector4": return new Vector4(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3)); case "Vector4i": return new Vector4I(AsI(a, 0), AsI(a, 1), AsI(a, 2), AsI(a, 3)); case "Color": return new Color(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3, 1f)); case "Rect2": return new Rect2(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3)); case "Rect2i": return new Rect2I(AsI(a, 0), AsI(a, 1), AsI(a, 2), AsI(a, 3)); case "Transform2D": return new Transform2D(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3), AsF(a, 4), AsF(a, 5)); case "Plane": return new Plane(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3)); case "Quaternion": return new Quaternion(AsF(a, 0), AsF(a, 1), AsF(a, 2), AsF(a, 3)); case "AABB": return new Aabb(new Vector3(AsF(a, 0), AsF(a, 1), AsF(a, 2)), new Vector3(AsF(a, 3), AsF(a, 4), AsF(a, 5))); default: return default; } }
        private static Godot.Collections.Array<Variant> GodotValueArrayToVariant(ParsedGodotValue[] arr) { var r = new Godot.Collections.Array<Variant>(); foreach (var pgv in arr) r.Add(GodotValueToVariant(pgv)); return r; }
        private static float AsF(List<object> a, int i, float fb = 0f) => i >= a.Count ? fb : a[i] is float f ? f : a[i] is int iv ? iv : a[i] is double d ? (float)d : fb;
        private static int AsI(List<object> a, int i, int fb = 0) => i >= a.Count ? fb : a[i] is int iv ? iv : a[i] is float f ? (int)f : fb;
        private static Godot.Collections.Array<string> CreateArray(string[] a) { var r = new Godot.Collections.Array<string>(); foreach (var x in a) r.Add(x); return r; }
        private static Godot.Collections.Array<int> CreateArray(int[] a) { var r = new Godot.Collections.Array<int>(); foreach (var x in a) r.Add(x); return r; }
        private static Godot.Collections.Array<long> CreateArray(long[] a) { var r = new Godot.Collections.Array<long>(); foreach (var x in a) r.Add(x); return r; }
        private static Godot.Collections.Array<float> CreateArray(float[] a) { var r = new Godot.Collections.Array<float>(); foreach (var x in a) r.Add(x); return r; }
        private static Godot.Collections.Array<byte> CreateArray(byte[] a) { var r = new Godot.Collections.Array<byte>(); foreach (var x in a) r.Add(x); return r; }
    }
}
