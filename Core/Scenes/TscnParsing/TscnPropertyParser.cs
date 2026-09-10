using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Parses property value strings from .tscn files. Deliberately avoids creating Godot native objects —
    /// returns <see cref="ParsedGodotValue"/> wrappers instead, so the parser can run in unit tests.
    /// Godot object construction is deferred to <see cref="TscnBuild.SubResourceFactory.ToVariant"/>.
    /// </summary>
    public static class TscnPropertyParser
    {
        /// <summary>Parses a raw .tscn value string. Returns null, bool, int, float, string, ParsedGodotValue, TscnPropertyValue, or arrays.</summary>
        public static object Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var trimmed = raw.Trim();
            if (trimmed == "null") return null;
            if (trimmed == "true") return true;
            if (trimmed == "false") return false;
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) return Unescape(trimmed.Substring(1, trimmed.Length - 2));
            if (trimmed.StartsWith("ExtResource(") && trimmed.EndsWith(")")) { var inner = trimmed.Substring("ExtResource(".Length, trimmed.Length - "ExtResource(".Length - 1); return TscnPropertyValue.FromExtResource(StripQuotes(inner.Trim())); }
            if (trimmed.StartsWith("SubResource(") && trimmed.EndsWith(")")) { var inner = trimmed.Substring("SubResource(".Length, trimmed.Length - "SubResource(".Length - 1); return TscnPropertyValue.FromSubResource(StripQuotes(inner.Trim())); }
            if (trimmed.StartsWith("NodePath(") && trimmed.EndsWith(")")) { var inner = trimmed.Substring("NodePath(".Length, trimmed.Length - "NodePath(".Length - 1); return ParsedGodotValue.Create("NodePath", StripQuotes(inner.Trim())); }
            if (trimmed.StartsWith("StringName(") && trimmed.EndsWith(")")) { var inner = trimmed.Substring("StringName(".Length, trimmed.Length - "StringName(".Length - 1); return ParsedGodotValue.Create("StringName", StripQuotes(inner.Trim())); }
            if (TryParseConstructorArgs("Vector2", trimmed, out var a2, 2)) return ParsedGodotValue.Create("Vector2", a2.ToArray());
            if (TryParseConstructorArgs("Vector2i", trimmed, out var a2i, 2)) return ParsedGodotValue.Create("Vector2i", a2i.ToArray());
            if (TryParseConstructorArgs("Vector3", trimmed, out var a3, 3)) return ParsedGodotValue.Create("Vector3", a3.ToArray());
            if (TryParseConstructorArgs("Vector3i", trimmed, out var a3i, 3)) return ParsedGodotValue.Create("Vector3i", a3i.ToArray());
            if (TryParseConstructorArgs("Vector4", trimmed, out var a4, 4)) return ParsedGodotValue.Create("Vector4", a4.ToArray());
            if (TryParseConstructorArgs("Vector4i", trimmed, out var a4i, 4)) return ParsedGodotValue.Create("Vector4i", a4i.ToArray());
            if (TryParseConstructorArgs("Color", trimmed, out var ca, 3, 4)) return ParsedGodotValue.Create("Color", ca.ToArray());
            if (TryParseConstructorArgs("Rect2", trimmed, out var r2, 4)) return ParsedGodotValue.Create("Rect2", r2.ToArray());
            if (TryParseConstructorArgs("Rect2i", trimmed, out var r2i, 4)) return ParsedGodotValue.Create("Rect2i", r2i.ToArray());
            if (TryParseConstructorArgs("Transform2D", trimmed, out var t2, 6)) return ParsedGodotValue.Create("Transform2D", t2.ToArray());
            if (TryParseConstructorArgs("Plane", trimmed, out var pa, 4)) return ParsedGodotValue.Create("Plane", pa.ToArray());
            if (TryParseConstructorArgs("Quaternion", trimmed, out var qa, 4)) return ParsedGodotValue.Create("Quaternion", qa.ToArray());
            if (TryParseConstructorArgs("AABB", trimmed, out var aa, 6)) return ParsedGodotValue.Create("AABB", aa.ToArray());
            if (trimmed.StartsWith("PackedStringArray(")) return ParsePackedArray<string>(trimmed, "PackedStringArray", ParseStringElement);
            if (trimmed.StartsWith("PackedInt32Array(")) return ParsePackedArray<int>(trimmed, "PackedInt32Array", s => int.Parse(s, CultureInfo.InvariantCulture));
            if (trimmed.StartsWith("PackedInt64Array(")) return ParsePackedArray<long>(trimmed, "PackedInt64Array", s => long.Parse(s, CultureInfo.InvariantCulture));
            if (trimmed.StartsWith("PackedFloat32Array(")) return ParsePackedArray<float>(trimmed, "PackedFloat32Array", s => float.Parse(s, CultureInfo.InvariantCulture));
            if (trimmed.StartsWith("PackedFloat64Array(")) return ParsePackedArray<double>(trimmed, "PackedFloat64Array", s => double.Parse(s, CultureInfo.InvariantCulture));
            if (trimmed.StartsWith("PackedByteArray(")) return ParsePackedArray<byte>(trimmed, "PackedByteArray", s => byte.Parse(s, CultureInfo.InvariantCulture));
            if (trimmed.StartsWith("PackedVector2Array(")) return ParsePackedGodotArray(trimmed, "PackedVector2Array", "Vector2", 2);
            if (trimmed.StartsWith("PackedVector3Array(")) return ParsePackedGodotArray(trimmed, "PackedVector3Array", "Vector3", 3);
            if (trimmed.StartsWith("PackedColorArray(")) return ParsePackedGodotArray(trimmed, "PackedColorArray", "Color", 4);
            if (trimmed.Contains("Array[") && trimmed.Contains("](")) return ParseTypedArray(trimmed);
            if (TryParseInt(trimmed, out var iv)) return iv;
            if (TryParseFloat(trimmed, out var fv)) return fv;
            return trimmed;
        }

        /// <summary>Splits a comma-separated argument list respecting nested parens/brackets/quotes. Used by section parsers for PackedStringArray, groups, and constructors.</summary>
        internal static List<string> SplitArgs(string inner)
        {
            var result = new List<string>();
            var depth = 0;
            var inString = false;
            var start = 0;
            for (var i = 0; i < inner.Length; i++)
            {
                var ch = inner[i];
                if (inString) { if (ch == '\\' && i + 1 < inner.Length) { i++; continue; } if (ch == '"') inString = false; continue; }
                if (ch == '"') { inString = true; continue; }
                if (ch == '(' || ch == '[' || ch == '{') { depth++; continue; }
                if (ch == ')' || ch == ']' || ch == '}') { depth--; continue; }
                if (depth == 0 && ch == ',') { result.Add(inner.Substring(start, i - start)); start = i + 1; }
            }
            if (start < inner.Length) result.Add(inner.Substring(start));
            return result;
        }

        private static string StripQuotes(string s) => s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\"") ? Unescape(s.Substring(1, s.Length - 2)) : s;
        private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\\\", "\\");
        private static bool TryParseInt(string s, out int v) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        private static bool TryParseFloat(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static bool TryParseConstructorArgs(string name, string expr, out List<object> args, int exact, int max = -1)
        {
            args = null; if (max < 0) max = exact;
            var pfx = name + "("; if (!expr.StartsWith(pfx) || !expr.EndsWith(")")) return false;
            var inner = expr.Substring(pfx.Length, expr.Length - pfx.Length - 1);
            var strs = SplitArgs(inner); if (strs.Count < exact || strs.Count > max) return false;
            args = new List<object>();
            foreach (var a in strs) { if (float.TryParse(a.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) args.Add(f); else args.Add(a.Trim()); }
            return true;
        }

        private static T[] ParsePackedArray<T>(string expr, string name, Func<string, T> parser)
        {
            var inner = expr.Substring(name.Length + 1, expr.Length - name.Length - 2);
            var strs = SplitArgs(inner); var r = new T[strs.Count];
            for (var i = 0; i < strs.Count; i++) r[i] = parser(StripQuotes(strs[i].Trim()));
            return r;
        }

        private static ParsedGodotValue[] ParsePackedGodotArray(string expr, string arrName, string elemName, int ac)
        {
            var inner = expr.Substring(arrName.Length + 1, expr.Length - arrName.Length - 2);
            var elems = SplitTopLevelArgs(inner, ac); var r = new ParsedGodotValue[elems.Count];
            for (var i = 0; i < elems.Count; i++)
            {
                var sa = SplitArgs(elems[i]); var fa = new List<object>();
                for (var j = 0; j < Math.Min(ac, sa.Count); j++)
                    fa.Add(float.TryParse(sa[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? (object)f : sa[j].Trim());
                r[i] = ParsedGodotValue.Create(elemName, fa.ToArray());
            }
            return r;
        }

        private static List<string> SplitTopLevelArgs(string expr, int elemArgCount)
        {
            var r = new List<string>(); var d = 0; var ca = 0; var inS = false; var s = 0;
            for (var i = 0; i < expr.Length; i++)
            {
                var ch = expr[i];
                if (inS) { if (ch == '\\' && i + 1 < expr.Length) { i++; continue; } if (ch == '"') inS = false; continue; }
                if (ch == '"') { inS = true; continue; } if (ch == '(') { d++; continue; } if (ch == ')') { d--; continue; }
                if (d == 0 && ch == ',') { ca++; if (ca >= elemArgCount) { r.Add(expr.Substring(s, i - s).Trim()); s = i + 1; ca = 0; } }
            }
            if (s < expr.Length) r.Add(expr.Substring(s).Trim());
            return r;
        }

        private static string ParseStringElement(string s) => StripQuotes(s);
        private static string[] ParseTypedArray(string expr) { var be = expr.IndexOf("]("); if (be < 0) return Array.Empty<string>(); var ps = be + 1; var inner = expr.Substring(ps + 1, expr.Length - ps - 2); return SplitArgs(inner).ToArray(); }
    }
}
