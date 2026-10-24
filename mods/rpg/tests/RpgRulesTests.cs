namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Walking rules and random battles: the terrain decisions the world makes on every step.</summary>
    public class RpgRulesTests
    {
        private static RpgTileset Tileset() => new()
        {
            ID = "test",
            Tiles = new List<RpgTileInfo>
            {
                new() { Index = 5, Solid = true },
                new() { Index = 9, Solid = false },
            },
        };

        [ModTest("rules/solid_tile_blocks_on_back_and_main_only")]
        static void SolidTileBlocksOnBackAndMainOnly(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(3, 1);
            grid.SetTile(RpgLayer.Back, 0, 0, 5);
            grid.SetTile(RpgLayer.Main, 1, 0, 5);
            grid.SetTile(RpgLayer.Front, 2, 0, 5);

            ModAssert.False(RpgPassability.IsWalkable(grid, Tileset(), 0, 0), "solid on back");
            ModAssert.False(RpgPassability.IsWalkable(grid, Tileset(), 1, 0), "solid on main");
            ModAssert.True(RpgPassability.IsWalkable(grid, Tileset(), 2, 0), "front tiles are walked under");
            ModAssert.False(RpgPassability.IsWalkable(grid, Tileset(), 3, 0), "outside the map");
        }

        [ModTest("rules/collision_layer_overrides_tiles")]
        static void CollisionLayerOverridesTiles(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(2, 1);
            grid.SetTile(RpgLayer.Main, 0, 0, 5);
            grid.SetCollision(0, 0, RpgCollision.Open);
            grid.SetCollision(1, 0, RpgCollision.Block);

            ModAssert.True(RpgPassability.IsWalkable(grid, Tileset(), 0, 0), "open beats a solid tile");
            ModAssert.False(RpgPassability.IsWalkable(grid, Tileset(), 1, 0), "block on empty ground");
        }

        [ModTest("rules/tileset_solid_lookup")]
        static void TilesetSolidLookup(ModTestContext ctx)
        {
            var tileset = Tileset();
            ModAssert.True(tileset.IsSolid(5), "listed solid");
            ModAssert.False(tileset.IsSolid(9), "listed walkable");
            ModAssert.False(tileset.IsSolid(1), "unlisted");
            ModAssert.False(tileset.IsSolid(-1), "empty cell");
        }

        [ModTest("rules/tileset_light_and_autotile_lookup_round_trip")]
        static void TilesetLightAndAutotileLookup(ModTestContext ctx)
        {
            var tileset = System.Text.Json.JsonSerializer.Deserialize<RpgTileset>(@"{
                ""Id"": ""lit"", ""Tiles"": [
                  { ""Index"": 29, ""Tag"": ""lamp_top"", ""Light"": { ""Radius"": 4, ""Color"": ""#ffcf80"" } },
                  { ""Index"": 40, ""Solid"": true, ""Tag"": ""water"", ""Autotile"": ""village/water_auto"" }
                ] }", Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);
            tileset = System.Text.Json.JsonSerializer.Deserialize<RpgTileset>(System.Text.Json.JsonSerializer.Serialize(tileset), Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);

            ModAssert.CloseTo(4, tileset.LightOf(29).Radius, what: "the lamp's light survives the round trip");
            ModAssert.Equal("#ffcf80", tileset.LightOf(29).Color, "with its colour");
            ModAssert.Null(tileset.LightOf(40), "the water gives no light");
            ModAssert.Null(tileset.LightOf(-1), "an empty cell gives no light");
            ModAssert.Equal("village/water_auto", tileset.AutotileOf(40)?.Autotile, "the autotile entry and its block");
            ModAssert.Null(tileset.AutotileOf(29), "a plain tile is no autotile");
            ModAssert.Null(tileset.AutotileOf(-1), "nor an empty cell");
            ModAssert.True(tileset.IsSolid(40), "an autotile is solid like any tile");
        }

        [ModTest("rules/encounter_off_without_table")]
        static void EncounterOffWithoutTable(ModTestContext ctx)
        {
            var random = new Random(1);
            ModAssert.Null(RpgEncounterRoller.Roll(null, random), "no table");
            ModAssert.Null(RpgEncounterRoller.Roll(new RpgEncounterTable { Steps = 0, Groups = new() { "a" } }, random), "steps 0");
            ModAssert.Null(RpgEncounterRoller.Roll(new RpgEncounterTable { Steps = 1 }, random), "no groups");
        }

        [ModTest("rules/encounter_every_step_picks_a_group")]
        static void EncounterEveryStepPicksAGroup(ModTestContext ctx)
        {
            var table = new RpgEncounterTable { Steps = 1, Groups = new() { "slime", "wolf,slime" } };
            var random = new Random(7);
            var seen = new HashSet<string>();
            for (var i = 0; i < 50; i++)
                seen.Add(RpgEncounterRoller.Roll(table, random));

            ModAssert.Equal(2, seen.Count, "both groups are picked, never null");
            ModAssert.True(seen.Contains("wolf,slime"), "groups come back as written");
        }

        [ModTest("rules/encounter_rate_follows_steps")]
        static void EncounterRateFollowsSteps(ModTestContext ctx)
        {
            var table = new RpgEncounterTable { Steps = 10, Groups = new() { "slime" } };
            var random = new Random(3);
            var battles = 0;
            for (var i = 0; i < 10000; i++)
            {
                if (RpgEncounterRoller.Roll(table, random) != null)
                    battles++;
            }
            ModAssert.True(battles > 800 && battles < 1200, $"about one battle in ten steps, got {battles}");
        }

        [ModTest("rules/directions")]
        static void Directions(ModTestContext ctx)
        {
            ModAssert.Equal(-1, RpgDirections.Delta(RpgDirection.Up).Y, "up is y-1");
            ModAssert.Equal(1, RpgDirections.Delta(RpgDirection.Right).X, "right is x+1");
            ModAssert.Equal("Up", RpgDirections.Opposite(RpgDirection.Down).ToString(), "opposite of down");
            ModAssert.Equal("Left", RpgDirections.Opposite(RpgDirection.Right).ToString(), "opposite of right");
        }
    }
}
