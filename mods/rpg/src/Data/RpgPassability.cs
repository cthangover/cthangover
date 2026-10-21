namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Whether the terrain of a cell can be walked on: the collision layer
    /// decides when painted, otherwise a solid tile on the back or the main layer
    /// blocks. Front-layer tiles never block — they are what the player walks
    /// under. Events are not terrain: the world adds their cells on top.
    /// </summary>
    public static class RpgPassability
    {
        /// <summary>True when the cell is inside the map and its terrain lets the player in.</summary>
        public static bool IsWalkable(RpgMapGrid grid, RpgTileset tileset, int x, int y)
        {
            if (grid == null || !grid.InBounds(x, y))
                return false;

            var code = grid.GetCollision(x, y);
            if (code == RpgCollision.Block)
                return false;
            if (code == RpgCollision.Open)
                return true;

            if (tileset == null)
                return true;
            return !tileset.IsSolid(grid.GetTile(RpgLayer.Back, x, y))
                && !tileset.IsSolid(grid.GetTile(RpgLayer.Main, x, y));
        }
    }
}
