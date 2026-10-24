namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>Autotile cells are assembled from quarters of the A2 block picked by their neighbours.</summary>
    public class RpgAutotilesTests
    {
        private const int Water = 40;

        private static bool Is(Vector2I[] quarters, params int[] xy)
        {
            for (var i = 0; i < 4; i++)
            {
                if (quarters[i] != new Vector2I(xy[i * 2], xy[i * 2 + 1]))
                    return false;
            }
            return true;
        }

        [ModTest("autotiles/quarters_by_neighbours")]
        static void QuartersByNeighbours(ModTestContext ctx)
        {
            ModAssert.True(Is(RpgAutotiles.Quarters(false, false, false, false, false, false, false, false), 0, 2, 3, 2, 0, 5, 3, 5),
                "alone: the four outer corners");
            ModAssert.True(Is(RpgAutotiles.Quarters(true, true, true, true, true, true, true, true), 2, 4, 1, 4, 2, 3, 1, 3),
                "surrounded: the fill of the patch");
            ModAssert.True(Is(RpgAutotiles.Quarters(true, true, true, true, false, false, false, false), 2, 0, 3, 0, 2, 1, 3, 1),
                "sides alike, corners not: the inner corners");
            ModAssert.True(Is(RpgAutotiles.Quarters(false, true, true, true, false, false, true, true), 2, 2, 1, 2, 2, 3, 1, 3),
                "the top row of a pond: top edge above, fill below");
            ModAssert.True(Is(RpgAutotiles.Quarters(true, true, false, true, false, true, false, true), 0, 4, 1, 4, 0, 3, 1, 3),
                "the left column: left edge");
            var corner = RpgAutotiles.Quarters(true, true, true, true, false, true, true, true);
            ModAssert.True(corner[0] == new Vector2I(2, 0) && corner[3] == new Vector2I(1, 3), "only the missing corner turns inner");
        }

        [ModTest("autotiles/cells_read_their_layer_and_the_map_edge_counts_alike")]
        static void QuartersAt(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(3, 3);
            grid.SetTile(RpgLayer.Back, 0, 0, Water);
            grid.SetTile(RpgLayer.Back, 1, 0, Water);
            grid.SetTile(RpgLayer.Main, 0, 1, Water);

            var corner = RpgAutotiles.QuartersAt(grid, RpgLayer.Back, 0, 0, Water);
            ModAssert.True(corner[0] == new Vector2I(2, 4), "off the map counts as water: the top-left quarter is fill");
            ModAssert.True(corner[2] == new Vector2I(2, 5), "below lies grass on this layer: bottom edge (the main layer is not read)");
            ModAssert.True(corner[1] == new Vector2I(1, 4), "to the right is water");

            var end = RpgAutotiles.QuartersAt(grid, RpgLayer.Back, 1, 0, Water);
            ModAssert.True(end[1] == new Vector2I(3, 4), "the right end: right edge");
            ModAssert.True(end[3] == new Vector2I(3, 5), "and its outer bottom-right corner");
        }

        [ModTest("autotiles/source_and_preview_rects")]
        static void Rects(ModTestContext ctx)
        {
            ModAssert.True(RpgAutotiles.SourceRect(new Vector2I(3, 5), 32) == new Rect2(48, 80, 16, 16), "quarters are half tiles");
            ModAssert.True(RpgAutotiles.SourceRect(Vector2I.Zero, 48) == new Rect2(0, 0, 24, 24), "of any tile size");
            ModAssert.True(RpgAutotiles.PreviewRect(32) == new Rect2(0, 0, 32, 32), "the preview is the top-left tile");
        }
    }
}
