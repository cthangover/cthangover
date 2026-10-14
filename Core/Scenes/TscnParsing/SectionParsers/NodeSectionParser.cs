using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing.SectionParsers
{
    /// <summary>
    /// Parses <c>[node]</c> — the core section. Extracts name, type, parent, instance (PackedScene ref), script (C# class ref),
    /// groups, node_paths. Special property handling: <c>script=</c> stored as ScriptRef, <c>__entry_point=</c> extracted
    /// to <see cref="ParsedScene.EntryPointClass"/> on root nodes, node_path vars stored as raw strings for build-time resolution.
    /// </summary>
    public class NodeSectionParser : ISectionParser
    {
        /// <summary>Section type name handled by this parser: <c>node</c>.</summary>
        public string SectionType => "node";
        private ParsedNode _current;

        /// <summary>Starts a node from the heading attributes: name, type, parent, instance, script, index, groups, node_paths.</summary>
        public void Open(Dictionary<string, string> h)
        {
            _current = new ParsedNode
            {
                Name = h.GetValueOrDefault("name"),
                Type = h.GetValueOrDefault("type"),
                Parent = h.GetValueOrDefault("parent"), 
                InstanceRef = h.GetValueOrDefault("instance"), 
                ScriptRef = h.GetValueOrDefault("script")
            };
            if (h.TryGetValue("index", out var rawIndex) && int.TryParse(rawIndex, out var index))
                _current.Index = index;
            if (h.TryGetValue("groups", out var gr))
                _current.Groups = ParseGroupArray(gr);
            if (h.TryGetValue("node_paths", out var np))
            {
                var vars = ParseGroupArray(np); _current.NodePathVars = new Dictionary<string, string>();
                foreach (var v in vars) _current.NodePathVars[v] = null;
            }
        }

        /// <summary>Routes <c>script=</c> into <see cref="ParsedNode.ScriptRef"/>, node-path variables into their dictionary, and everything else into the ordered property map.</summary>
        public void Property(string key, string rawValue)
        {
            if (_current == null)
                return;
            if (key == "script")
            {
                _current.ScriptRef = rawValue; return;
            }
            var pv = TscnPropertyParser.Parse(rawValue);
            if (_current.NodePathVars != null && _current.NodePathVars.ContainsKey(key))
            {
                _current.NodePathVars[key] = rawValue; return;
            }
            _current.Properties[key] = pv;
            _current.PropertyOrder.Add(key);
        }

        /// <summary>Extracts <c>__entry_point</c> from root nodes into <see cref="ParsedScene.EntryPointClass"/> and appends the node to the scene.</summary>
        public void Close(ParsedScene s)
        {
            if (_current == null) return;
            if (string.IsNullOrEmpty(s.EntryPointClass) && (_current.Parent == null || _current.Parent == ".") && _current.Properties.TryGetValue("__entry_point", out var ep) && ep is string ec)
            {
                s.EntryPointClass = ec; _current.Properties.Remove("__entry_point");
            }
            s.Nodes.Add(_current); _current = null;
        }

        private static string[] ParseGroupArray(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return new string[0];
            var t = raw.Trim();
            if (t.StartsWith("[") && t.EndsWith("]"))
                t = t.Substring(1, t.Length - 2);
            if (t.StartsWith("PackedStringArray(") && t.EndsWith(")"))
                t = t.Substring("PackedStringArray(".Length, t.Length - "PackedStringArray(".Length - 1);
            var items = new List<string>();
            foreach (var p in TscnPropertyParser.SplitArgs(t))
            {
                var c = p.Trim();
                if (c.StartsWith("\"") && c.EndsWith("\""))
                    c = c.Substring(1, c.Length - 2);
                if (!string.IsNullOrEmpty(c))
                    items.Add(c);
            }
            return items.ToArray();
        }
    }
}
