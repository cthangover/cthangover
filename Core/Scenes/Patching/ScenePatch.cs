using Cthangover.Core.Scenes.TscnParsing;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// One parsed scene patch — an inherited scene whose root instances the target.
    /// Produced by <see cref="ScenePatchParser"/> and cached by <see cref="ScenePatchResolver"/>;
    /// applied to a target <see cref="ParsedScene"/> by <see cref="ScenePatchApplier"/>.
    /// The parsed <see cref="Scene"/> is treated as immutable: the applier deep-clones the layer
    /// before touching it, so the cached parse can be reused across scene loads.
    /// </summary>
    public class ScenePatch
    {
        /// <summary>Id of the mod that shipped the patch file (layer order and diagnostics).</summary>
        public string ModId { get; set; }

        /// <summary>Mod-relative path of the patch file itself.</summary>
        public string SourcePath { get; set; }

        /// <summary>Canonical target scene key (<c>scenes/menu/main_menu.tscn</c>).</summary>
        public string TargetPath { get; set; }

        /// <summary>
        /// Local id of the patch's own <c>PackedScene</c> ext_resource — the header reference to
        /// the target. The applier skips it when folding patch resources into the target so the
        /// base scene does not gain a self-referencing resource.
        /// </summary>
        public string TargetExtResourceId { get; set; }

        /// <summary>Raw parsed patch content.</summary>
        public ParsedScene Scene { get; set; }
    }
}
