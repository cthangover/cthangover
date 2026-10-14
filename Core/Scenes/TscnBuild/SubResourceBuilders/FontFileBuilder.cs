using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="FontFile"/>. Also serves "Font" alias (mapped by SubResourceFactory).</summary>
    public class FontFileBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="FontFile"]</c> (plus the "Font" alias mapped by SubResourceFactory).</summary>
        public string ResourceType => "FontFile";
        /// <summary>Creates a <see cref="FontFile"/> and applies the parsed font data properties.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var f = new FontFile(); SubResourceFactory.ApplyProperties(f, def); return f; }
    }
}
