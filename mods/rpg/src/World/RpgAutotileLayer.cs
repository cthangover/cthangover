using System.Collections.Generic;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// The autotile cells of one map layer, drawn as a child of that layer's
    /// <see cref="TileMapLayer"/> — right above its plain tiles, below the next layer — with
    /// the layer's material (the map's light). A tile map cannot hold them: each cell is four
    /// quarters picked from its neighbours (<see cref="RpgAutotiles"/>), so the cells and their
    /// quarters are worked out once when the map is built and drawn by <c>_Draw</c>, which
    /// Godot caches until the next redraw.
    /// </summary>
    public partial class RpgAutotileLayer : Node2D
    {
        private readonly List<(Rect2 Cell, Texture2D Block, Vector2I[] Quarters)> cells = new();
        private readonly int tileSize;

        /// <summary>Collects the autotile cells of <paramref name="layer"/>; cells whose picture is missing stay empty.</summary>
        public RpgAutotileLayer(RpgMapGrid grid, RpgLayer layer, RpgTileset tileset, RpgTextures textures)
        {
            Name = layer + "Autotiles";
            UseParentMaterial = true;
            tileSize = tileset.TileSize;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetTile(layer, x, y);
                    var info = tileset.AutotileOf(index);
                    var block = info == null ? null : textures.Tileset(info.Autotile);
                    if (block == null)
                        continue;
                    var rect = new Rect2(x * tileSize, y * tileSize, tileSize, tileSize);
                    cells.Add((rect, block, RpgAutotiles.QuartersAt(grid, layer, x, y, index)));
                }
            }
        }

        /// <summary>Number of drawn autotile cells.</summary>
        public int Count => cells.Count;

        /// <summary>Draws every collected cell.</summary>
        public override void _Draw()
        {
            foreach (var (cell, block, quarters) in cells)
                RpgAutotiles.Draw(this, block, cell, quarters, tileSize, Colors.White);
        }
    }
}
