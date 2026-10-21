using System.Collections.Generic;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// The three drawn layers of a map, each a list of rows. A row is a string of
    /// space-separated atlas indices, <c>.</c> for an empty cell, so a map file
    /// stays readable and diffable by hand (see <see cref="RpgMapGrid"/>).
    /// <list type="bullet">
    /// <item><see cref="Back"/> — ground: grass, floors, water.</item>
    /// <item><see cref="Main"/> — objects at the actors' level: walls, trunks, fences.</item>
    /// <item><see cref="Front"/> — drawn over the actors: tree crowns, roofs' eaves, arches.</item>
    /// </list>
    /// </summary>
    public class RpgMapLayers
    {
        /// <summary>Ground layer rows.</summary>
        public List<string> Back { get; set; } = new();

        /// <summary>Object layer rows.</summary>
        public List<string> Main { get; set; } = new();

        /// <summary>Overhead layer rows.</summary>
        public List<string> Front { get; set; } = new();
    }
}
