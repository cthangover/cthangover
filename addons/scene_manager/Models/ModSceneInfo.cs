#if TOOLS
using System.Collections.Generic;

namespace SceneManagerAddon
{
    /// <summary>
    /// Represents a single mod's scene manifest — the collection of all
    /// <see cref="SceneDefInfo"/> definitions discovered in the mod's
    /// <c>scenes/</c> directory. Produced by <see cref="Services.SceneDataLoader.LoadAll"/>
    /// and consumed throughout the plugin tree, graph, and validation views.
    /// </summary>
    public sealed class ModSceneInfo
    {
        /// <summary>
        /// The directory name of the mod under <c>res://mods/</c> (e.g. <c>"core"</c>).
        /// Used as the primary mod identifier when resolving resources and
        /// correlating scenarios to their owning mod.
        /// </summary>
        public string ModId { get; set; }

        /// <summary>
        /// The <c>res://</c>-relative path to the mod's root directory (e.g. <c>"res://mods/core"</c>).
        /// </summary>
        public string ModPath { get; set; }

        /// <summary>
        /// All parsed scene definitions belonging to this mod, each carrying
        /// their own scenario sub-lists and validation errors.
        /// </summary>
        public List<SceneDefInfo> Scenes { get; set; } = new();
    }
}
#endif
