using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="StyleBoxEmpty"/> — a transparent placeholder style box.</summary>
    public class StyleBoxEmptyBuilder : ISubResourceBuilder
    {
        public string ResourceType => "StyleBoxEmpty";
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var b = new StyleBoxEmpty(); SubResourceFactory.ApplyProperties(b, def); return b; }
    }
}
