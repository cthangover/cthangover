using Cthangover.Mods.Rpg;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>One undo step of <see cref="RpgMapDocument"/>: the cells and the rest of the map as JSON.</summary>
    public class RpgMapSnapshot
    {
        /// <summary>Copy of the cells.</summary>
        public RpgMapGrid Grid { get; }

        /// <summary>The map without its cells (rows may be stale), serialized.</summary>
        public string MapJson { get; }

        /// <summary>Creates a step.</summary>
        public RpgMapSnapshot(RpgMapGrid grid, string mapJson)
        {
            Grid = grid;
            MapJson = mapJson;
        }
    }
}
