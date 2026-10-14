using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
    /// <summary>Builds <see cref="ImageTexture"/> from sub_resource properties.</summary>
    public class ImageTextureBuilder : ISubResourceBuilder
    {
        /// <summary>Handles <c>[sub_resource type="ImageTexture"]</c> (e.g. editor-embedded images).</summary>
        public string ResourceType => "ImageTexture";
        /// <summary>Creates an <see cref="ImageTexture"/> and applies the parsed image properties.</summary>
        public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var t = new ImageTexture(); SubResourceFactory.ApplyProperties(t, def); return t; }
    }
}
