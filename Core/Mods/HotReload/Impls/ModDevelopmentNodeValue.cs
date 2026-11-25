namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Text.Json.Nodes;
    using Godot;

    /// <summary>Maps JSON values to the native type declared by a Godot node property.</summary>
    internal static class ModDevelopmentNodeValue
    {
        internal static Variant.Type WritableType(Node node, StringName name)
        {
            var properties = node.GetPropertyList();
            foreach (var property in properties)
            {
                if (property["name"].AsString() != name.ToString())
                    continue;

                var usage = (PropertyUsageFlags)property["usage"].AsInt64();
                if ((usage & PropertyUsageFlags.ReadOnly) != 0)
                    throw new ArgumentException("Read-only node property: " + name);
                return (Variant.Type)property["type"].AsInt32();
            }

            throw new ArgumentException("Unknown node property: " + name);
        }

        internal static Variant Read(JsonNode json, Variant.Type type) => type switch
        {
            Variant.Type.Bool => json.GetValue<bool>(),
            Variant.Type.Int => json.GetValue<long>(),
            Variant.Type.Float => Number(json),
            Variant.Type.String => json.GetValue<string>(),
            Variant.Type.StringName => new StringName(json.GetValue<string>()),
            Variant.Type.NodePath => new NodePath(json.GetValue<string>()),
            Variant.Type.Vector2 => Vector2Value(Coordinates(json, 2)),
            Variant.Type.Vector3 => Vector3Value(Coordinates(json, 3)),
            Variant.Type.Vector4 => Vector4Value(Coordinates(json, 4)),
            Variant.Type.Color => ColorValue(Coordinates(json, 4)),
            Variant.Type.Quaternion => Rotation(Coordinates(json, 4)),
            _ => throw new ArgumentException("Unsupported node property type: " + type)
        };

        internal static bool CanTween(Variant.Type type) => type is Variant.Type.Float or Variant.Type.Int
            or Variant.Type.Vector2 or Variant.Type.Vector3 or Variant.Type.Vector4 or Variant.Type.Color
            or Variant.Type.Quaternion;

        internal static Variant Offset(Variant from, Variant delta, int sign) => from.VariantType switch
        {
            Variant.Type.Float => from.AsDouble() + sign * delta.AsDouble(),
            Variant.Type.Vector2 => from.AsVector2() + sign * delta.AsVector2(),
            Variant.Type.Vector3 => from.AsVector3() + sign * delta.AsVector3(),
            Variant.Type.Vector4 => from.AsVector4() + sign * delta.AsVector4(),
            _ => throw new ArgumentException("Sine motion needs a float or vector property.")
        };

        internal static double Number(JsonNode value)
        {
            var number = value.GetValue<double>();
            return double.IsFinite(number) ? number : throw new ArgumentException("Expected a finite number.");
        }

        private static float[] Coordinates(JsonNode value, int count)
        {
            if (value is not JsonArray array || array.Count != count)
                throw new ArgumentException("Expected an array of " + count + " components.");

            var components = new float[count];
            for (var index = 0; index < count; index++)
            {
                components[index] = (float)Number(array[index] ?? throw new ArgumentException("A component is missing."));
                if (!float.IsFinite(components[index]))
                    throw new ArgumentException("A component exceeds the finite Godot coordinate range.");
            }
            return components;
        }

        private static Vector2 Vector2Value(float[] a) => new(a[0], a[1]);
        private static Vector3 Vector3Value(float[] a) => new(a[0], a[1], a[2]);
        private static Vector4 Vector4Value(float[] a) => new(a[0], a[1], a[2], a[3]);
        private static Color ColorValue(float[] a) => new(a[0], a[1], a[2], a[3]);

        private static Quaternion Rotation(float[] a)
        {
            var rotation = new Quaternion(a[0], a[1], a[2], a[3]);
            var length = rotation.LengthSquared();
            if (!float.IsFinite(length) || length < 1e-10f)
                throw new ArgumentException("A rotation quaternion needs a finite, nonzero length.");
            return rotation.Normalized();
        }
    }
}
