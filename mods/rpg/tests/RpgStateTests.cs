namespace Cthangover.Mods.Rpg.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>The saved position and the map file format survive a JSON round trip.</summary>
    public class RpgStateTests
    {
        [ModTest("state/save_and_load_round_trip")]
        static void SaveAndLoadRoundTrip(ModTestContext ctx)
        {
            var state = RpgState.Instance;
            var before = state.Data;
            try
            {
                state.Place("rpg_forest", 4, 7, RpgDirection.Left);
                var slot = new ModSlot();
                var saveable = new RpgSaveable();
                saveable.OnSave(slot);

                var json = JsonSerializer.Serialize(slot);
                var restored = JsonSerializer.Deserialize<ModSlot>(json);
                state.Reset();
                ModAssert.True(saveable.OnLoad(restored, out var error), error);

                ModAssert.Equal("rpg_forest", state.Data.Map, "map");
                ModAssert.Equal(4, state.Data.X, "x");
                ModAssert.Equal(7, state.Data.Y, "y");
                ModAssert.Equal("Left", state.Data.Dir.ToString(), "facing");
            }
            finally
            {
                state.Data = before;
            }
        }

        [ModTest("state/load_without_data_goes_to_spawn")]
        static void LoadWithoutDataGoesToSpawn(ModTestContext ctx)
        {
            var state = RpgState.Instance;
            var before = state.Data;
            try
            {
                state.Place("rpg_forest", 1, 1, RpgDirection.Up);
                ModAssert.True(new RpgSaveable().OnLoad(new ModSlot(), out _), "an empty slot is accepted");
                ModAssert.Null(state.Data.Map, "no map: the next entry uses the spawn");
            }
            finally
            {
                state.Data = before;
            }
        }

        [ModTest("state/map_file_round_trip")]
        static void MapFileRoundTrip(ModTestContext ctx)
        {
            var json = @"{""Id"":""m"",""Tileset"":""village"",""Width"":2,""Height"":1,
                ""Spawn"":{""X"":1,""Y"":0,""Dir"":""left""},
                ""Events"":[{""Id"":""door"",""X"":0,""Y"":0,""Trigger"":""touch"",""Teleport"":{""Map"":""x"",""X"":3,""Y"":4,""Dir"":""Up""}}],
                ""Encounters"":{""Steps"":20,""Groups"":[""slime""]}}";

            var map = JsonSerializer.Deserialize<RpgMap>(json, Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);
            var again = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map), Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);

            ModAssert.Equal("m", again.ID, "id");
            ModAssert.Equal("Left", again.Spawn.Dir.ToString(), "spawn facing, read in any case");
            ModAssert.Equal("Touch", again.Events[0].Trigger.ToString(), "trigger");
            ModAssert.Equal("x", again.Events[0].Teleport.Map, "teleport map");
            ModAssert.Equal(4, again.Events[0].Teleport.Y, "teleport y");
            ModAssert.False(again.Events[0].IsSolid, "an invisible event does not block");
            ModAssert.Equal(20, again.Encounters.Steps, "encounter steps");
            ModAssert.Equal(2, again.Zoom, "zoom default");
        }
    }
}
