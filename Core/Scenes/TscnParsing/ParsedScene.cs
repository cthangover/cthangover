using System.Collections.Generic;

namespace Cthangover.Core.Scenes.TscnParsing
{
    /// <summary>
    /// Intermediate representation of a fully parsed .tscn scene file.
    /// Contains flat lists of resources, nodes, and connections — the node hierarchy
    /// is encoded via <see cref="ParsedNode.Parent"/> references, not a tree.
    /// </summary>
    public class ParsedScene
    {
        public string Format { get; set; }
        public List<ExtResourceRef> ExtResources { get; set; } = new();
        public List<SubResourceDef> SubResources { get; set; } = new();
        public List<ParsedNode> Nodes { get; set; } = new();
        public List<ParsedConnection> Connections { get; set; } = new();
        public string EntryPointClass { get; set; }
    }

    /// <summary>Parsed [ext_resource] entry. Stores type ("Script"/"PackedScene"), mod-relative path, and local id.</summary>
    public class ExtResourceRef
    {
        public string Type { get; set; }
        public string Path { get; set; }
        public string Id { get; set; }
    }

    /// <summary>Parsed [sub_resource] section. Inline resources (StyleBoxFlat, Gradient, etc.), built later by <see cref="TscnBuild.ISubResourceBuilder"/>.</summary>
    public class SubResourceDef
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        /// <summary>Keys of <see cref="Properties"/> in source-file order. Godot applies properties in file order, which matters for layout/anchors and sub-resource cross-references.</summary>
        public List<string> PropertyOrder { get; set; } = new();

        /// <summary>Properties in the order they appeared in the .tscn source.</summary>
        public IEnumerable<KeyValuePair<string, object>> OrderedProperties
        {
            get
            {
                foreach (var key in PropertyOrder)
                    if (Properties.TryGetValue(key, out var value))
                        yield return new KeyValuePair<string, object>(key, value);
            }
        }
    }

    /// <summary>
    /// Parsed [node] section. Nodes are stored flat — parent-child relationships via <see cref="Parent"/>:
    /// null = root, "." = direct child of root, "Name" or "Name/Child" = explicit path.
    /// <see cref="InstanceRef"/> and <see cref="ScriptRef"/> store raw ExtResource strings, resolved at build time.
    /// </summary>
    public class ParsedNode
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Parent { get; set; }
        public string InstanceRef { get; set; }
        public string ScriptRef { get; set; }
        public string[] Groups { get; set; }
        public Dictionary<string, string> NodePathVars { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        /// <summary>Keys of <see cref="Properties"/> in source-file order. Godot applies node properties in file order — critical for <c>layout_mode</c> → <c>anchors_preset</c> → <c>anchor_*</c>/<c>offset_*</c> sequencing.</summary>
        public List<string> PropertyOrder { get; set; } = new();

        /// <summary>Properties in the order they appeared in the .tscn source.</summary>
        public IEnumerable<KeyValuePair<string, object>> OrderedProperties
        {
            get
            {
                foreach (var key in PropertyOrder)
                    if (Properties.TryGetValue(key, out var value))
                        yield return new KeyValuePair<string, object>(key, value);
            }
        }
    }

    /// <summary>Parsed [connection] section. Only script-method connections (to=".") are kept; visual/animation connections are filtered during parsing.</summary>
    public class ParsedConnection
    {
        public string Signal { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Method { get; set; }
        public int Flags { get; set; }
    }
}
