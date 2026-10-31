using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="FontFile"/>.</summary>
    public class FontFileBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="FontFile"]</c>.</summary>
        public string ResourceType => "FontFile";
        /// <summary>Creates a <see cref="FontFile"/> and applies the parsed font data properties.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var f = new FontFile(); SubResourceFactory.ApplyProperties(f, def); return f; }
    }
}
