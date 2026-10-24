namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Properties of one atlas cell of an <see cref="RpgTileset"/>. Only cells that
    /// differ from the default (walkable, unnamed) need an entry.
    /// </summary>
    public class RpgTileInfo
    {
        /// <summary>Row-major atlas index: <c>row * Columns + column</c>.</summary>
        public int Index { get; set; }

        /// <summary>
        /// The tile blocks walking when it lies on the back or the main layer
        /// (front-layer tiles never block: the player walks under them). The map's
        /// collision layer can override it per cell.
        /// </summary>
        public bool Solid { get; set; }

        /// <summary>
        /// Optional author-facing tag shown by the editor palette ("grass",
        /// "tree_crown"). Not a locale key: players never see it.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>Light the tile gives off wherever it lies on a map (a lamp, a lit window); null = none.</summary>
        public RpgLight Light { get; set; }

        /// <summary>
        /// Picture of an autotile block (RPG Maker A2 layout, see <see cref="RpgAutotiles"/>) in the
        /// tilesets group; set, the entry is an autotile and <see cref="Index"/> should lie outside the atlas.
        /// </summary>
        public string Autotile { get; set; }
    }
}
