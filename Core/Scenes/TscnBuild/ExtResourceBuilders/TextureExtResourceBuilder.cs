using System.IO;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.ExtResourceBuilders
{
    /// <summary>Builds <see cref="Texture2D"/> from raw image bytes (PNG, JPG, WebP).</summary>
    public class TextureExtResourceBuilder : IExtResourceBuilder
    {
        public string ResourceType => "Texture2D";

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
