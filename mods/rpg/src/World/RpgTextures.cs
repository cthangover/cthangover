using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Image lookup of the RPG world: tileset atlases in the <c>tilesets</c> file
    /// group, charsets in <c>charsets</c>. Both are file groups (last mod wins),
    /// not texture groups, so their names never collide with backgrounds or
    /// icons. Decoded textures are kept for the life of one world, which builds
    /// and owns an instance; the editor keeps its own.
    /// </summary>
    public class RpgTextures
    {
        /// <summary>File group of tileset images.</summary>
        public const string TilesetGroup = "tilesets";

        /// <summary>File group of charset images.</summary>
        public const string CharsetGroup = "charsets";

        private readonly Dictionary<string, Texture2D> cache = new();

        /// <summary>Tileset atlas image, or null (logged by the loader).</summary>
        public Texture2D Tileset(string path) => Load(TilesetGroup, path);

        /// <summary>Charset image, or null.</summary>
        public Texture2D Charset(string path) => Load(CharsetGroup, path);

        private Texture2D Load(string group, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            var key = group + "/" + path;
            if (!cache.TryGetValue(key, out var texture))
            {
                texture = TextureUtils.LoadFromModGroup(group, path);
                cache[key] = texture;
            }
            return texture;
        }
    }
}
