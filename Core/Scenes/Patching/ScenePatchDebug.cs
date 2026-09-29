using System.Text;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Minimal scene-patch debugging aid: renders the effective scene — the result after all
    /// patch layers were applied — as text and writes it through <see cref="GameLogger"/>.
    /// Patch layers are invisible in the Godot editor (each patch sees only the base scene), so
    /// when a mash-up of several mods behaves unexpectedly this dump is the ground truth for
    /// what the builder will actually receive. <see cref="Format"/> is pure and reused by tests.
    /// </summary>
    public static class ScenePatchDebug
    {
        private const string LogCategory = "TSCN";

        /// <summary>Formats the effective scene as an indented node tree plus resources and connections.</summary>
        public static string Format(ParsedScene scene)
        {
            if (scene == null)
                return "<null scene>";

            var sb = new StringBuilder();
            sb.AppendLine($"scene: format={scene.Format ?? "?"}, entry_point={scene.EntryPointClass ?? "none"}");
            sb.AppendLine($"nodes ({scene.Nodes.Count}):");

            foreach (var node in scene.Nodes)
            {
                var path = NodePathOf(node);
                var kind = !string.IsNullOrEmpty(node.InstanceRef)
                    ? $"instance={node.InstanceRef}"
                    : $"type={node.Type ?? "?"}";

                sb.Append("  ").Append(path.Length == 0 ? "<root>" : path).Append(" [").Append(kind);
                if (!string.IsNullOrEmpty(node.ScriptRef))
                    sb.Append(", script=").Append(node.ScriptRef);
                if (node.Index.HasValue)
                    sb.Append(", index=").Append(node.Index.Value);
                sb.AppendLine("]");

                foreach (var key in node.PropertyOrder)
                    sb.Append("      ").Append(key).Append(" = ").AppendLine(Describe(node.Properties.TryGetValue(key, out var v) ? v : null));

                if (node.Groups is { Length: > 0 })
                    sb.Append("      groups = [").Append(string.Join(", ", node.Groups)).AppendLine("]");
            }

            sb.AppendLine($"ext_resources ({scene.ExtResources.Count}):");
            foreach (var ext in scene.ExtResources)
                sb.Append("  ").Append(ext.Id).Append(" -> ").Append(ext.Type).Append(' ').AppendLine(ext.Path);

            sb.AppendLine($"sub_resources ({scene.SubResources.Count}):");
            foreach (var sub in scene.SubResources)
                sb.Append("  ").Append(sub.Id).Append(" : ").AppendLine(sub.Type);

            sb.AppendLine($"connections ({scene.Connections.Count}):");
            foreach (var conn in scene.Connections)
                sb.Append("  ").Append(conn.From).Append('.').Append(conn.Signal).Append(" -> ").Append(conn.To).Append('.').AppendLine(conn.Method);

            return sb.ToString().TrimEnd();
        }

        /// <summary>Logs the effective scene under a label; no-op-safe outside the runtime.</summary>
        public static void DumpEffective(ParsedScene scene, string label)
        {
            GameLogger.Log(LogCategory, $"ScenePatchDebug: effective scene '{label}'\n{Format(scene)}");
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case TscnPropertyValue property:
                    return $"{property.Type}({property.RefId})";
                case ParsedGodotValue godotValue:
                    return $"{godotValue.TypeName}({string.Join(", ", godotValue.Args)})";
                case string[] strings:
                    return "[" + string.Join(", ", strings) + "]";
                default:
                    return value.ToString();
            }
        }

        private static string NodePathOf(ParsedNode node)
        {
            if (string.IsNullOrEmpty(node.Parent))
                return "";
            if (node.Parent == "." || node.Parent == "..")
                return node.Name ?? "";
            return node.Parent.TrimEnd('/') + "/" + node.Name;
        }
    }
}
