using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="StyleBoxEmpty"/> — a transparent placeholder style box.</summary>
    public class StyleBoxEmptyBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="StyleBoxEmpty"]</c> — the transparent placeholder style box.</summary>
        public string ResourceType => "StyleBoxEmpty";
        /// <summary>Creates a <see cref="StyleBoxEmpty"/>; there are no meaningful properties to apply.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var b = new StyleBoxEmpty(); SubResourceFactory.ApplyProperties(b, def); return b; }
    }
}
