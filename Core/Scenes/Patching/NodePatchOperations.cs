namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Reserved operations extracted from one layer node before it is merged.
    /// <see cref="ScenePatchApplier"/> keeps them in a dictionary keyed by the layer node
    /// instead of adding patch-only fields to the parser's <see cref="TscnParsing.ParsedNode"/>,
    /// so the parsing model stays a faithful representation of the file.
    /// </summary>
    internal sealed class NodePatchOperations
    {
        /// <summary><c>metadata/_patch_delete</c>: remove the target node (cascades).</summary>
        public bool Delete;

        /// <summary><c>metadata/_patch_remove</c>: property key masks removed before values merge.</summary>
        public System.Collections.Generic.List<string> Remove;

        /// <summary><c>metadata/_patch_add_groups</c>.</summary>
        public System.Collections.Generic.List<string> AddGroups;

        /// <summary><c>metadata/_patch_remove_groups</c>.</summary>
        public System.Collections.Generic.List<string> RemoveGroups;

        /// <summary><c>metadata/_patch_delete_nodes</c> (root only): node path masks.</summary>
        public System.Collections.Generic.List<string> DeleteNodes;

        /// <summary><c>metadata/_patch_remove_connections</c> (root only): connection selectors.</summary>
        public System.Collections.Generic.List<string> RemoveConnections;

        /// <summary><c>metadata/_patch_remove_ext_resources</c> (root only): ext_resource ids.</summary>
        public System.Collections.Generic.List<string> RemoveExtResources;
    }
}
