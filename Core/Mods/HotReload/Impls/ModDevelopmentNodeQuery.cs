namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Godot;

    /// <summary>Reads explicitly requested Godot properties and managed instance fields on a live node.</summary>
    public sealed class ModDevelopmentNodeQuery : IModDevelopmentCommand
    {
        /// <summary>Exposes a property and field snapshot of one live node.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "node.inspect" };

        /// <summary>Identifies the tree path and available reflection selectors.</summary>
        public string Description { get; } = "node.inspect: reads requested Godot properties and managed instance fields of a live node by tree path.";

        /// <summary>Reads the selected node through the development command registry.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
            => Task.FromResult<JsonNode>(InspectNode(context));

        internal static JsonObject InspectNode(ModDevelopmentContext context)
        {
            using var path = new NodePath(context.Require("path"));
            var request = context.Request;
            var node = context.Tree.Root.GetNodeOrNull(path) ?? throw new ArgumentException("Node does not exist: " + path);
            var wanted = request["properties"]?.Deserialize<string[]>();
            if (wanted?.Length > 64)
                throw new ArgumentException("Request at most 64 properties.");
            var properties = new JsonObject();
            if (wanted != null)
            {
                var available = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in node.GetPropertyList())
                    available.Add(property["name"].AsString());
                foreach (var name in wanted)
                {
                    if (!available.Contains(name))
                        throw new ArgumentException("Node has no property '" + name + "'.");
                    properties[name] = InspectValue(node.Get(name));
                }
            }
            return new JsonObject
            {
                ["path"] = node.GetPath().ToString(),
                ["id"] = node.GetInstanceId(),
                ["type"] = node.GetType().FullName,
                ["children"] = JsonSerializer.SerializeToNode(node.GetChildren().Take(128).Select(child => child.Name.ToString())),
                ["properties"] = properties,
                ["fields"] = InspectFields(node, request["fields"])
            };
        }

        private static JsonObject InspectFields(Node node, JsonNode request)
        {
            var result = new JsonObject();
            var names = request?.Deserialize<string[]>();
            if (names == null)
                return result;
            if (names.Length > 64)
                throw new ArgumentException("Request at most 64 managed fields.");
            foreach (var name in names)
            {
                FieldInfo field = null;
                for (var type = node.GetType(); type != null && type.Assembly != typeof(Node).Assembly; type = type.BaseType)
                {
                    field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                        break;
                }
                if (field == null)
                    throw new ArgumentException("Node has no managed instance field '" + name + "'.");
                result[name] = InspectManagedValue(field.GetValue(node));
            }
            return result;
        }

        private static JsonNode InspectManagedValue(object value)
        {
            return value switch
            {
                null => null,
                string text => JsonValue.Create(text.Length > 4096 ? text[..4096] : text),
                bool or int or uint or long or ulong or short or ushort or byte or sbyte or decimal => JsonSerializer.SerializeToNode(value),
                float number when float.IsFinite(number) => JsonValue.Create(number),
                double number when double.IsFinite(number) => JsonValue.Create(number),
                Enum enumeration => JsonValue.Create(enumeration.ToString()),
                Node node => new JsonObject { ["type"] = node.GetType().FullName, ["path"] = node.IsInsideTree() ? node.GetPath().ToString() : null },
                System.Collections.ICollection collection => new JsonObject { ["type"] = value.GetType().FullName, ["count"] = collection.Count },
                _ => new JsonObject { ["type"] = value.GetType().FullName }
            };
        }

        private static JsonNode InspectValue(Variant value)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Nil: return null;
                case Variant.Type.Bool: return JsonValue.Create(value.AsBool());
                case Variant.Type.Int: return JsonValue.Create(value.AsInt64());
                case Variant.Type.Float when double.IsFinite(value.AsDouble()): return JsonValue.Create(value.AsDouble());
                case Variant.Type.Array: return new JsonObject { ["type"] = "Array", ["count"] = value.AsGodotArray().Count };
                case Variant.Type.Dictionary: return new JsonObject { ["type"] = "Dictionary", ["count"] = value.AsGodotDictionary().Count };
                case Variant.Type.Object:
                    return value.AsGodotObject() is Node node
                        ? new JsonObject { ["type"] = node.GetType().FullName, ["path"] = node.IsInsideTree() ? node.GetPath().ToString() : null }
                        : new JsonObject { ["type"] = value.AsGodotObject()?.GetType().FullName };
                case Variant.Type.String:
                case Variant.Type.StringName:
                case Variant.Type.NodePath:
                case Variant.Type.Vector2:
                case Variant.Type.Vector3:
                case Variant.Type.Vector4:
                case Variant.Type.Color:
                case Variant.Type.Transform2D:
                case Variant.Type.Transform3D:
                    var text = value.AsString();
                    return JsonValue.Create(text.Length > 4096 ? text[..4096] : text);
                default: return new JsonObject { ["type"] = value.VariantType.ToString() };
            }
        }
    }
}
