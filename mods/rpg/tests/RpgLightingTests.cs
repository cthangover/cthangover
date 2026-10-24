namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>The map's light: ambient weights from the clock, lit tiles, the fixed ambient of a map.</summary>
    public class RpgLightingTests
    {
        [ModTest("lights/weights_follow_clock_dark_mode_and_switch")]
        static void WeightsFollowClock(ModTestContext ctx)
        {
            ModAssert.CloseTo(1, RpgLighting.Weights(13f, false, true).Y, what: "noon is day");
            ModAssert.CloseTo(1, RpgLighting.Weights(0.5f, false, true).W, what: "midnight is night");
            ModAssert.CloseTo(1, RpgLighting.Weights(13f, true, true).W, what: "dark mode is night at noon");
            ModAssert.CloseTo(1, RpgLighting.Weights(0.5f, false, false).Y, what: "lighting switched off keeps day at midnight");
            var dawn = RpgLighting.Weights(5f, false, true);
            ModAssert.CloseTo(1, dawn.X + dawn.W, what: "dawn blends night into morning");
            ModAssert.True(dawn.X > 0 && dawn.W > 0, "both phases at dawn");
        }

        [ModTest("lights/tile_lights_on_any_layer_once_per_cell")]
        static void TileLights(ModTestContext ctx)
        {
            var tileset = new RpgTileset
            {
                ID = "lit",
                Tiles = new List<RpgTileInfo>
                {
                    new() { Index = 3, Light = new RpgLight { Radius = 4 } },
                    new() { Index = 7, Light = new RpgLight { Radius = 1.5f, Color = "#ffd27a" } },
                    new() { Index = 9, Solid = true },
                },
            };
            var grid = new RpgMapGrid(4, 3);
            grid.SetTile(RpgLayer.Front, 1, 0, 3);
            grid.SetTile(RpgLayer.Main, 2, 2, 7);
            grid.SetTile(RpgLayer.Back, 2, 2, 3);
            grid.SetTile(RpgLayer.Main, 0, 1, 9);

            var lights = RpgLighting.TileLights(grid, tileset);
            ModAssert.Equal(2, lights.Count, "one light per lit cell, unlit tiles give none");
            ModAssert.True(lights.Exists(l => l.Cell == new Vector2I(1, 0) && Mathf.IsEqualApprox(l.Light.Radius, 4f)), "a front-layer lamp");
            ModAssert.True(lights.Exists(l => l.Cell == new Vector2I(2, 2)), "a cell lit on two layers counts once");
        }

        [ModTest("lights/map_ambient_json")]
        static void MapAmbientJson(ModTestContext ctx)
        {
            var room = JsonSerializer.Deserialize<RpgMap>(@"{ ""ID"": ""room"", ""Ambient"": ""#e8dcc8"" }", JsonOptionsProvider.ReadOptions);
            ModAssert.Equal("#e8dcc8", room.Ambient, "a lit room");
            var again = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(room), JsonOptionsProvider.ReadOptions);
            ModAssert.Equal("#e8dcc8", again.Ambient, "ambient survives the round trip");
            ModAssert.Null(JsonSerializer.Deserialize<RpgMap>(@"{ ""ID"": ""field"" }", JsonOptionsProvider.ReadOptions).Ambient, "outdoors by default");
        }
    }
}
