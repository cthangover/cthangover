using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Specialised <c>PrefabFactory</c> for <c>.png</c> and <c>.jpg</c> textures.
    /// Creates a temporary <c>Image</c> to decode pixel data via Godot's
    /// built-in loaders, then immediately wraps it in an <c>ImageTexture</c>
    /// and disposes the intermediate <c>Image</c>. This two-step pipeline is
    /// necessary because Godot does not provide a direct "byte array to
    /// Texture2D" method — the <c>Image</c> holds CPU-side pixel data while
    /// <c>ImageTexture</c> uploads it to the GPU, and keeping the <c>Image</c>
    /// alive would double memory usage for every loaded sprite.
    /// </summary>
    public abstract class Texture2DFactory : PrefabFactory<Texture2D>
    {
        protected Texture2DFactory(string factoryKey, int fallbackCacheSize)
            : base(factoryKey, fallbackCacheSize) { }

        protected override List<string> Extensions => ModConfig.Instance.TextureExtensions;
        
        protected override Texture2D ConvertFromBytes(string id, byte[] data, string extension)
        {
            var image = new Image();
            if (extension?.Equals(".png", StringComparison.OrdinalIgnoreCase) == true)
                image.LoadPngFromBuffer(data);
            else if (extension?.Equals(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
                     extension?.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) == true)
            {
                image.LoadJpgFromBuffer(data);
            }
            else
            {
                GameLogger.Log("FACTORY", $"unsupported image format '{extension}' for '{id}'", LogLevel.Error);
                return null;
            }
            var texture = ImageTexture.CreateFromImage(image);
            image.Dispose();
            return texture;
        }

    }
}
