using System.IO;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.ExtResourceBuilders
{
    /// <summary>Builds <see cref="Texture2D"/> from raw image bytes (PNG, JPG, WebP).</summary>
    public class TextureExtResourceBuilder : IExtResourceBuilder
    {
        /// <summary>Handles <c>[ext_resource type="Texture2D"]</c> entries (the registry's default for unhandled types).</summary>
        public string ResourceType => "Texture2D";

        /// <summary>Decodes PNG/JPG/WebP bytes into an <see cref="ImageTexture"/>; other formats return null.</summary>
        public Resource Build(byte[] data, string path)
        {
            var img = new Image();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".png") img.LoadPngFromBuffer(data);
            else if (ext == ".jpg" || ext == ".jpeg") img.LoadJpgFromBuffer(data);
            else if (ext == ".webp") img.LoadWebpFromBuffer(data);
            else return null;
            return ImageTexture.CreateFromImage(img);
        }
    }
}
