namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Reserved <c>metadata/_patch_*</c> property keys consumed by <see cref="ScenePatchApplier"/>.
    /// A scene patch is a normal Godot inherited scene, so every operation Godot can express
    /// (property overrides, added nodes, sibling order, groups, resources, connections) is written
    /// natively; only operations inheritance cannot express — deleting nodes, deleting inherited
    /// properties, mask-based bulk edits, resource GC — are encoded as these metadata markers.
    /// <para/>
    /// The keys are stripped from the parsed scene before it reaches <see cref="TscnBuild.SceneBuilder"/>,
    /// so they never become Godot node metadata. The whole <c>metadata/_patch_*</c> namespace is
    /// reserved: a regular mod using such keys for its own metadata will conflict.
    /// </summary>
    public static class PatchMetadata
    {
        /// <summary>Prefix of every reserved key.</summary>
        public const string Prefix = "metadata/_patch_";

        /// <summary>Bool on an override node or sub_resource body: remove the target node/sub_resource (nodes cascade to descendants, connections and node-path bindings).</summary>
        public const string Delete = Prefix + "delete";

        /// <summary>String or string list on an override node: delete matching target properties (glob masks, <c>"*"</c> = all).</summary>
        public const string Remove = Prefix + "remove";

        /// <summary>String list on an override node: add groups to the target node.</summary>
        public const string AddGroups = Prefix + "add_groups";

        /// <summary>String list on an override node: remove groups from the target node.</summary>
        public const string RemoveGroups = Prefix + "remove_groups";

        /// <summary>String list on the patch root: delete target nodes by path masks (paths are relative to the root, e.g. <c>MenuContainer/*Btn</c>; the root itself is never matched).</summary>
        public const string DeleteNodes = Prefix + "delete_nodes";

        /// <summary>String list on the patch root: connection selectors (<c>from=...;signal=...;method=...</c>) removing every matching connection.</summary>
        public const string RemoveConnections = Prefix + "remove_connections";

        /// <summary>String list on the patch root: ids of ext_resources to remove when nothing references them anymore.</summary>
        public const string RemoveExtResources = Prefix + "remove_ext_resources";

        /// <summary>Bool on the patch root: run resource garbage collection once after all layers have been applied.</summary>
        public const string Gc = Prefix + "gc";
    }
}
