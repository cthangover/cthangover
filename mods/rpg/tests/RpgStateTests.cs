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
                ""Encounters"":{""Steps"":20,""Groups"":[""slime""]}}";

            var map = JsonSerializer.Deserialize<RpgMap>(json, Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);
            var again = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map), Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);

            ModAssert.Equal("m", again.ID, "id");
            ModAssert.Equal("Left", again.Spawn.Dir.ToString(), "spawn facing, read in any case");
            ModAssert.Equal(20, again.Encounters.Steps, "encounter steps");
            ModAssert.Equal("slime", again.Encounters.Groups[0], "encounter group");
            ModAssert.Equal(2, again.Zoom, "zoom default");
        }

        [ModTest("state/event_file_round_trip")]
        static void EventFileRoundTrip(ModTestContext ctx)
        {
            var json = @"{""Events"":[
                {""Id"":""door"",""X"":0,""Y"":0,""Trigger"":""touch"",""Teleport"":{""Map"":""x"",""X"":3,""Y"":4,""Dir"":""Up""}},
                {""Id"":""herb"",""X"":2,""Y"":1,""Icon"":""rpg_demo/forest_mushroom"",""Commands"":[""end""],""Condition"":""herb != 1""}]}";

            var file = JsonSerializer.Deserialize<RpgMapEventFile>(json, Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);
            file.Events[0].ModId = "some_mod";
            var text = JsonSerializer.Serialize(file);
            var again = JsonSerializer.Deserialize<RpgMapEventFile>(text, Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);

            ModAssert.Equal(2, again.Events.Count, "both events");
            ModAssert.Equal("Touch", again.Events[0].Trigger.ToString(), "trigger");
            ModAssert.Equal("x", again.Events[0].Teleport.Map, "teleport map");
            ModAssert.Equal(4, again.Events[0].Teleport.Y, "teleport y");
            ModAssert.False(again.Events[0].IsSolid, "an invisible event does not block");
            ModAssert.Equal("rpg_demo/forest_mushroom", again.Events[1].Icon, "icon");
            ModAssert.True(again.Events[1].IsSolid, "an icon is drawn, so it blocks unless told otherwise");
            ModAssert.Equal("herb != 1", again.Events[1].Condition, "condition");
            ModAssert.Equal("end", again.Events[1].Commands[0], "commands");
            ModAssert.False(text.Contains("some_mod"), "where an event came from is not written");
        }

        [ModTest("state/event_merge_adds_and_replaces")]
        static void EventMergeAddsAndReplaces(ModTestContext ctx)
        {
            var merge = new RpgMapEventMerge();
            merge.Add("village", "base_game", "map_events/village/events.json", new[]
            {
                new RpgMapEvent { Id = "elder", X = 1, Y = 1 },
                new RpgMapEvent { Id = "sign", X = 2, Y = 1 },
                new RpgMapEvent { Id = " " }
            });
            var replaced = merge.Add("VILLAGE", "quest_mod", "map_events/village/quest.json", new[]
            {
                new RpgMapEvent { Id = "Elder", X = 5, Y = 5 },
                new RpgMapEvent { Id = "quest_giver", X = 3, Y = 3 }
            });
            merge.Add("forest", "quest_mod", "map_events/forest/a.json", null);

            var village = merge.Maps["village"];
            ModAssert.Equal(3, village.Count, "an event without an id is skipped, a new id is added");
            ModAssert.Equal(1, replaced.Count, "one replacement reported");
            ModAssert.Equal("Elder", replaced[0], "the replacing id");
            ModAssert.Equal(5, village[0].X, "the later mod's event replaces in place (any case)");
            ModAssert.Equal("quest_mod", village[0].ModId, "the replacement remembers its mod");
            ModAssert.Equal("sign", village[1].Id, "the others keep their order");
            ModAssert.Equal("base_game", village[1].ModId, "and their mod");
            ModAssert.Equal("map_events/village/quest.json", village[2].File, "an added event remembers its file");
            ModAssert.Equal(0, merge.Maps["forest"].Count, "a file without events adds nothing");
        }
    }
}
