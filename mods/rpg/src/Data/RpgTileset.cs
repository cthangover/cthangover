using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// A square-tile atlas: one image cut into <see cref="TileSize"/> cells,
    /// <see cref="Columns"/> per row, addressed by a row-major index. Maps store
    /// only these indices, so a pack can redraw its tiles without touching maps.
    /// <para/>
    /// Files declare <c>"Kind": "rpg_tileset"</c> and live anywhere in a mod; the
    /// image is resolved in the <c>tilesets</c> file group (last mod wins), so a
    /// dependent mod can reskin a tileset by shipping an image of the same path.
    /// </summary>
    [EntityKind("rpg_tileset")]
    public class RpgTileset : IIdentifiable
    {
        private HashSet<int> solid;

        /// <summary>Tileset id that maps reference.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Image path inside the <c>tilesets</c> group, extension optional.</summary>
        public string Image { get; set; }

        /// <summary>Edge of one square tile in image pixels.</summary>
        public int TileSize { get; set; } = 32;

        /// <summary>Atlas cells per row.</summary>
        public int Columns { get; set; } = 8;

        /// <summary>Per-cell properties; cells without an entry are walkable.</summary>
        public List<RpgTileInfo> Tiles { get; set; } = new();

        /// <summary>True when the atlas cell blocks walking. Built once from <see cref="Tiles"/>.</summary>
        public bool IsSolid(int index)
        {
            if (index < 0)
                return false;
            if (solid == null)
            {
                solid = new HashSet<int>();
                foreach (var tile in Tiles ?? new List<RpgTileInfo>())
                {
                    if (tile.Solid)
                        solid.Add(tile.Index);
                }
            }
            return solid.Contains(index);
        }
    }
}
