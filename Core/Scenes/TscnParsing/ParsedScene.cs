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
        /// <summary>Value of the <c>format=</c> attribute in [gd_scene]; information only (format 3 expected).</summary>
        public string Format { get; set; }
        /// <summary>All [ext_resource] entries in file order.</summary>
        public List<ExtResourceRef> ExtResources { get; set; } = new();
        /// <summary>All [sub_resource] entries in file order.</summary>
        public List<SubResourceDef> SubResources { get; set; } = new();
        /// <summary>All [node] sections in file order — the order is the build order.</summary>
        public List<ParsedNode> Nodes { get; set; } = new();
        /// <summary>Root-script [connection] sections kept by the parser.</summary>
        public List<ParsedConnection> Connections { get; set; } = new();
        /// <summary>Fully-qualified entry-point class from the root node's <c>__entry_point</c> property; null when absent.</summary>
        public string EntryPointClass { get; set; }
    }

    /// <summary>Parsed [ext_resource] entry. Stores type ("Script"/"PackedScene"), mod-relative path, and local id.</summary>
    public class ExtResourceRef
    {
        /// <summary>Declared resource type ("Script", "PackedScene", "Texture2D", …).</summary>
        public string Type { get; set; }
        /// <summary>Mod-relative (or normalized) path of the resource file.</summary>
        public string Path { get; set; }
        /// <summary>Local id referenced by <c>ExtResource("id")</c> within this scene file.</summary>
        public string Id { get; set; }
    }

    /// <summary>Parsed [sub_resource] section. Inline resources (StyleBoxFlat, Gradient, etc.), built later by <see cref="TscnBuild.ISubResourceBuilder"/>.</summary>
    public class SubResourceDef
    {
        /// <summary>Resource type name from the section header ("StyleBoxFlat", "Gradient", …).</summary>
        public string Type { get; set; }
        /// <summary>Local id referenced by <c>SubResource("id")</c>.</summary>
        public string Id { get; set; }
        /// <summary>Parsed property values keyed by property name.</summary>
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
        /// <summary>Node name from the section header.</summary>
        public string Name { get; set; }
        /// <summary>Godot class name for new nodes; null on override/instance headings.</summary>
        public string Type { get; set; }
        /// <summary>Parent path: null for the root, "." for a direct child of root, "Name/Child" for deeper placement.</summary>
        public string Parent { get; set; }
        /// <summary>Raw <c>instance=ExtResource("id")</c> value for inherited nodes; resolved at build time.</summary>
        public string InstanceRef { get; set; }
        /// <summary>Raw <c>script=ExtResource("id")</c> value; resolved to a C# type at build time.</summary>
        public string ScriptRef { get; set; }
        /// <summary>
        /// Editor-written sibling position (<c>index="N"</c> in the [node] heading).
        /// Null for ordinary scenes; inherited scenes use it to order overridden and
        /// added nodes, which is why the scene patcher re-sorts siblings when any node sets it.
        /// </summary>
        public int? Index { get; set; }
        /// <summary>Group names from the <c>groups=[...]</c> heading attribute; null when absent.</summary>
        public string[] Groups { get; set; }
        /// <summary>Exported NodePath bindings declared by the <c>node_paths=[...]</c> heading, keyed by property name.</summary>
        public Dictionary<string, string> NodePathVars { get; set; } = new();
        /// <summary>Parsed property values keyed by property name (entry-point and patch metadata keys are removed before build).</summary>
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
        /// <summary>Signal name emitted by the source node.</summary>
        public string Signal { get; set; }
        /// <summary>Source node path relative to the root ("." = root itself).</summary>
        public string From { get; set; }
        /// <summary>Target node path ("." = root — the only form the parser keeps).</summary>
        public string To { get; set; }
        /// <summary>Method name invoked on the target node.</summary>
        public string Method { get; set; }
        /// <summary>Godot connect flags from the section header (default 0).</summary>
        public int Flags { get; set; }
    }
}
