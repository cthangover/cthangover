using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="FontFile"/>. Also serves "Font" alias (mapped by SubResourceFactory).</summary>
    public class FontFileBuilder : ISubResourceBuilder
    {
        public string ResourceType => "FontFile";
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var f = new FontFile(); SubResourceFactory.ApplyProperties(f, def); return f; }
    }
}
