using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="ImageTexture"/> from sub_resource properties.</summary>
    public class ImageTextureBuilder : ISubResourceBuilder
    {
        public string ResourceType => "ImageTexture";
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var t = new ImageTexture(); SubResourceFactory.ApplyProperties(t, def); return t; }
    }
}
