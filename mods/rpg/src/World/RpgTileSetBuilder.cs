using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Turns an <see cref="RpgTileset"/> and its atlas image into a Godot
    /// <see cref="TileSet"/> with a single atlas source (id 0), so maps are drawn
    /// by <see cref="TileMapLayer"/> — batched, culled, pixel-exact. Atlas index
    /// <c>i</c> is atlas coordinate <c>(i % Columns, i / Columns)</c>; the world
    /// and the editor both convert through <see cref="AtlasCoords"/>.
    /// </summary>
    public static class RpgTileSetBuilder
    {
        /// <summary>The only atlas source id of a built tile set.</summary>
        public const int SourceId = 0;

        /// <summary>Builds the tile set; every whole cell of the image becomes a tile.</summary>
        public static TileSet Build(RpgTileset tileset, Texture2D texture)
        {
            var size = new Vector2I(tileset.TileSize, tileset.TileSize);
            var tileSet = new TileSet { TileSize = size };
            var source = new TileSetAtlasSource { Texture = texture, TextureRegionSize = size };

            var columns = texture.GetWidth() / tileset.TileSize;
            var rows = texture.GetHeight() / tileset.TileSize;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                    source.CreateTile(new Vector2I(x, y));
            }

            tileSet.AddSource(source, SourceId);
            return tileSet;
        }

        /// <summary>Atlas coordinate of a row-major index.</summary>
        public static Vector2I AtlasCoords(RpgTileset tileset, int index)
        {
            var columns = System.Math.Max(1, tileset.Columns);
            return new Vector2I(index % columns, index / columns);
        }

        /// <summary>Number of tiles the image holds.</summary>
        public static int TileCount(RpgTileset tileset, Texture2D texture)
        {
            if (texture == null || tileset.TileSize <= 0)
                return 0;
            return (texture.GetWidth() / tileset.TileSize) * (texture.GetHeight() / tileset.TileSize);
        }
    }
}
