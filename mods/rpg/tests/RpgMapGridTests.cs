namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Map layers are hand-edited text: parsing must be forgiving and give a
    /// grid of exactly the declared size, and writing must read back the same.
    /// </summary>
    public class RpgMapGridTests
    {
        [ModTest("grid/parse_reads_rows_and_collision")]
        static void ParseReadsRowsAndCollision(ModTestContext ctx)
        {
            var map = new RpgMap
            {
                Width = 3,
                Height = 2,
                Layers = new RpgMapLayers
                {
                    Back = new List<string> { "0 1 2", "3 . 5" },
                    Main = new List<string> { ". 8" },
                    Front = new List<string>(),
                },
                Collision = new List<string> { "#o.", ".x" },
            };

            var grid = RpgMapGrid.Parse(map);

            ModAssert.Equal(1, grid.GetTile(RpgLayer.Back, 1, 0), "back (1,0)");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Back, 1, 1), "a dot is empty");
            ModAssert.Equal(8, grid.GetTile(RpgLayer.Main, 1, 0), "main (1,0)");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Main, 2, 1), "a missing row is empty");
            ModAssert.Equal("#", grid.GetCollision(0, 0).ToString(), "block code");
            ModAssert.Equal("o", grid.GetCollision(1, 0).ToString(), "open code");
            ModAssert.Equal(".", grid.GetCollision(1, 1).ToString(), "an unknown code is auto");
            ModAssert.Equal("#", grid.GetCollision(5, 5).ToString(), "outside the map is blocked");
        }

        [ModTest("grid/parse_is_forgiving")]
        static void ParseIsForgiving(ModTestContext ctx)
        {
            var map = new RpgMap
            {
                Width = 2,
                Height = 1,
                Layers = new RpgMapLayers { Back = new List<string> { "7 abc 9 9" } },
            };

            var grid = RpgMapGrid.Parse(map);

            ModAssert.Equal(2, grid.Width, "width stays declared");
            ModAssert.Equal(7, grid.GetTile(RpgLayer.Back, 0, 0), "first cell");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Back, 1, 0), "an unreadable token is empty");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Back, 2, 0), "extra cells are cut off");
        }

        [ModTest("grid/write_reads_back")]
        static void WriteReadsBack(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(3, 2);
            grid.SetTile(RpgLayer.Back, 0, 0, 12);
            grid.SetTile(RpgLayer.Main, 2, 1, 3);
            grid.SetTile(RpgLayer.Front, 1, 0, 0);
            grid.SetCollision(1, 1, RpgCollision.Block);

            var map = new RpgMap();
            grid.WriteTo(map);
            var back = RpgMapGrid.Parse(map);

            ModAssert.Equal(3, map.Width, "width written");
            ModAssert.Equal(2, map.Height, "height written");
            ModAssert.Equal("12  .  .", map.Layers.Back[0], "indices are aligned to one width");
            ModAssert.Equal(12, back.GetTile(RpgLayer.Back, 0, 0), "back");
            ModAssert.Equal(3, back.GetTile(RpgLayer.Main, 2, 1), "main");
            ModAssert.Equal(0, back.GetTile(RpgLayer.Front, 1, 0), "front tile 0 is not empty");
            ModAssert.Equal("#", back.GetCollision(1, 1).ToString(), "collision");
        }

        [ModTest("grid/all_auto_collision_writes_no_rows")]
        static void AllAutoCollisionWritesNoRows(ModTestContext ctx)
        {
            var map = new RpgMap();
            new RpgMapGrid(4, 4).WriteTo(map);

            ModAssert.Equal(0, map.Collision.Count, "no collision rows");
            ModAssert.Equal(4, map.Layers.Main.Count, "layer rows are always written");
        }

        [ModTest("grid/resize_keeps_top_left")]
        static void ResizeKeepsTopLeft(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(2, 2);
            grid.SetTile(RpgLayer.Back, 1, 1, 5);
            grid.SetCollision(0, 1, RpgCollision.Open);

            grid.Resize(4, 1);

            ModAssert.Equal(4, grid.Width, "new width");
            ModAssert.Equal(1, grid.Height, "new height");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Back, 1, 1), "a cut row is gone");
            ModAssert.Equal(-1, grid.GetTile(RpgLayer.Back, 3, 0), "new cells are empty");

            grid.Resize(2, 2);
            ModAssert.Equal(".", grid.GetCollision(0, 1).ToString(), "a row cut once does not come back");
        }

        [ModTest("grid/clone_is_independent")]
        static void CloneIsIndependent(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(2, 2);
            grid.SetTile(RpgLayer.Main, 0, 0, 4);

            var copy = grid.Clone();
            copy.SetTile(RpgLayer.Main, 0, 0, 9);
            copy.SetCollision(1, 1, RpgCollision.Block);

            ModAssert.Equal(4, grid.GetTile(RpgLayer.Main, 0, 0), "original tile");
            ModAssert.Equal(".", grid.GetCollision(1, 1).ToString(), "original collision");
        }
    }
}
