namespace Cthangover.Tools.RpgEditor.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg;

    /// <summary>The editor's document: brushes, events, undo and the saved file.</summary>
    public class RpgMapDocumentTests
    {
        private static RpgMapDocument NewMap() => RpgMapDocument.Create("test_map", "village", 5, 4, 0);

        [ModTest("document/create_fills_ground_and_centres_spawn")]
        static void CreateFillsGroundAndCentresSpawn(ModTestContext ctx)
        {
            var document = NewMap();
            ModAssert.Equal(0, document.Grid.GetTile(RpgLayer.Back, 4, 3), "ground everywhere");
            ModAssert.Equal(-1, document.Grid.GetTile(RpgLayer.Main, 0, 0), "main empty");
            ModAssert.Equal(2, document.Map.Spawn.X, "spawn x");
            ModAssert.Equal(2, document.Map.Spawn.Y, "spawn y");
        }

        [ModTest("document/paint_reports_real_changes")]
        static void PaintReportsRealChanges(ModTestContext ctx)
        {
            var document = NewMap();
            ModAssert.True(document.Paint(RpgEditLayer.Main, 1, 1, 8, RpgCollision.Auto), "a new tile");
            ModAssert.False(document.Paint(RpgEditLayer.Main, 1, 1, 8, RpgCollision.Auto), "the same tile again");
            ModAssert.False(document.Paint(RpgEditLayer.Main, 9, 9, 8, RpgCollision.Auto), "outside");
            ModAssert.True(document.Paint(RpgEditLayer.Collision, 2, 2, -1, RpgCollision.Block), "collision code");
            ModAssert.Equal("#", document.Grid.GetCollision(2, 2).ToString(), "collision written");
            ModAssert.False(document.Paint(RpgEditLayer.Events, 0, 0, 3, RpgCollision.Auto), "the events layer has no cells");
        }

        [ModTest("document/fill_stops_at_other_cells")]
        static void FillStopsAtOtherCells(ModTestContext ctx)
        {
            var document = NewMap();
            for (var y = 0; y < 4; y++)
                document.Paint(RpgEditLayer.Back, 2, y, 5, RpgCollision.Auto);

            var changed = document.Fill(RpgEditLayer.Back, 0, 0, 3, RpgCollision.Auto);

            ModAssert.Equal(8, changed, "the left two columns");
            ModAssert.Equal(3, document.Grid.GetTile(RpgLayer.Back, 1, 3), "filled");
            ModAssert.Equal(5, document.Grid.GetTile(RpgLayer.Back, 2, 0), "the wall stays");
            ModAssert.Equal(0, document.Grid.GetTile(RpgLayer.Back, 4, 0), "the other side stays");
            ModAssert.Equal(0, document.Fill(RpgEditLayer.Back, 0, 0, 3, RpgCollision.Auto), "filling with the same tile changes nothing");
        }

        [ModTest("document/undo_and_redo_restore_cells_and_events")]
        static void UndoAndRedoRestoreCellsAndEvents(ModTestContext ctx)
        {
            var document = NewMap();
            ModAssert.False(document.CanUndo, "fresh document");

            document.BeginChange();
            document.Paint(RpgEditLayer.Front, 0, 0, 9, RpgCollision.Auto);
            document.BeginChange();
            document.AddEvent(3, 1).Sprite = "village/elder";

            ModAssert.True(document.Undo(), "undo the event");
            ModAssert.Equal(0, document.Events.Count, "event gone");
            ModAssert.Equal(9, document.Grid.GetTile(RpgLayer.Front, 0, 0), "the earlier paint stays");
            ModAssert.True(document.Undo(), "undo the paint");
            ModAssert.Equal(-1, document.Grid.GetTile(RpgLayer.Front, 0, 0), "paint gone");
            ModAssert.False(document.Undo(), "nothing more");

            ModAssert.True(document.Redo(), "redo the paint");
            ModAssert.True(document.Redo(), "redo the event");
            ModAssert.Equal("village/elder", document.Events[0].Sprite, "event back with its data");

            document.Undo();
            document.BeginChange();
            ModAssert.False(document.CanRedo, "a new edit drops the redo branch");
        }

        [ModTest("document/events_get_unique_ids_and_stack")]
        static void EventsGetUniqueIdsAndStack(ModTestContext ctx)
        {
            var document = NewMap();
            var first = document.AddEvent(1, 1);
            var second = document.AddEvent(1, 1);

            ModAssert.Equal("event_1", first.Id, "first id");
            ModAssert.Equal("event_2", second.Id, "second id");
            ModAssert.Equal("event_2", document.EventsAt(1, 1)[0].Id, "the top event comes first");
            ModAssert.True(document.RemoveEvent(first), "removed");
            ModAssert.Equal("event_1", document.AddEvent(0, 0).Id, "a freed id is used again");
        }

        [ModTest("document/characters_become_npc_and_enemy_events")]
        static void CharactersBecomeNpcAndEnemyEvents(ModTestContext ctx)
        {
            var document = NewMap();
            var npc = document.AddCharacter(1, 2, "rpg_elder", "village/elder", false);
            var enemy = document.AddCharacter(3, 1, "rpg_slime", "village/slime", true);
            var second = document.AddCharacter(4, 1, "rpg_slime", null, true);

            ModAssert.Equal("rpg_elder", npc.Id, "the character id is the event id");
            ModAssert.Equal("village/elder", npc.Sprite, "the charset is the look");
            ModAssert.Equal(1, npc.X, "x");
            ModAssert.Equal(2, npc.Y, "y");
            ModAssert.True(npc.Commands == null && npc.Condition == null, "an NPC gets no lines and no condition");
            ModAssert.Equal("Action", npc.Trigger.ToString(), "talked to on E/F");

            ModAssert.Equal("action rpg.battle enemies=rpg_slime win_flag=test_map_rpg_slime_won", enemy.Commands[0], "an enemy starts a battle with itself");
            ModAssert.Equal("end", enemy.Commands[1], "and ends the dialog");
            ModAssert.Equal("test_map_rpg_slime_won != 1", enemy.Condition, "hidden once beaten");

            ModAssert.Equal("rpg_slime_2", second.Id, "a taken id gets a number");
            ModAssert.Null(second.Sprite, "no charset: no look");
            ModAssert.Equal("test_map_rpg_slime_2_won != 1", second.Condition, "each placed enemy is beaten separately");
            ModAssert.Equal(0, document.Validate().Count, "the ids are valid");

            var loaded = RpgMapDocument.Load(document.ToJson(), document.EventsToJson());
            var restored = loaded.Events[1];
            ModAssert.Equal("rpg_slime", restored.Id, "id survives the file");
            ModAssert.Equal("village/slime", restored.Sprite, "sprite survives the file");
            ModAssert.Equal(2, restored.Commands.Count, "commands survive the file");
            ModAssert.Equal(enemy.Commands[0], restored.Commands[0], "battle line survives the file");
            ModAssert.Equal(enemy.Condition, restored.Condition, "condition survives the file");
        }

        [ModTest("document/items_become_pickups")]
        static void ItemsBecomePickups(ModTestContext ctx)
        {
            var document = NewMap();
            var first = document.AddItem(2, 1, "rpg_demo/branches", "rpg_demo/branch_bundle", 3, "Found \"branches\"");
            var second = document.AddItem(3, 1, "rpg_demo/branches", null, 0, "x");

            ModAssert.Equal("rpg_demo_branches", first.Id, "the id holds no slash");
            ModAssert.Equal("rpg_demo/branch_bundle", first.Icon, "the item picture is the look");
            ModAssert.Equal("Touch", first.Trigger.ToString(), "picked up by stepping on it");
            ModAssert.False(first.IsSolid, "the player can step on it");
            ModAssert.Equal("ptext \"Found 'branches'\"", first.Commands[0], "the line, quotes made safe");
            ModAssert.Equal("action item.add id=rpg_demo/branches count=3", first.Commands[1], "adds the stack");
            ModAssert.Equal("flag test_map_rpg_demo_branches_taken 1", first.Commands[2], "remembers it was taken");
            ModAssert.Equal("end", first.Commands[3], "ends the dialog");
            ModAssert.Equal("test_map_rpg_demo_branches_taken != 1", first.Condition, "gone once taken");

            ModAssert.Equal("rpg_demo_branches_2", second.Id, "a taken id gets a number");
            ModAssert.Equal("action item.add id=rpg_demo/branches count=1", second.Commands[1], "at least one item");
            ModAssert.Equal("test_map_rpg_demo_branches_2_taken != 1", second.Condition, "each pickup has its own flag");

            var restored = RpgMapDocument.Load(document.ToJson(), document.EventsToJson()).Events[0];
            ModAssert.Equal("rpg_demo/branch_bundle", restored.Icon, "icon survives the file");
            ModAssert.Equal("Touch", restored.Trigger.ToString(), "trigger survives the file");
            ModAssert.False(restored.IsSolid, "solidity survives the file");
            ModAssert.Equal(4, restored.Commands.Count, "commands survive the file");
            ModAssert.Equal(first.Condition, restored.Condition, "condition survives the file");
        }

        [ModTest("document/locked_events_are_shown_not_edited")]
        static void LockedEventsAreShownNotEdited(ModTestContext ctx)
        {
            var document = NewMap();
            document.SetLockedEvents(new[]
            {
                new RpgMapEvent { Id = "event_1", X = 1, Y = 1, ModId = "quest_mod", File = "map_events/test_map/quest.json" },
                new RpgMapEvent { Id = "rpg_slime", X = 2, Y = 2, ModId = "quest_mod", File = "map_events/test_map/quest.json" }
            });

            ModAssert.Equal(0, document.EventsAt(1, 1).Count, "a locked event is not picked on its cell");
            ModAssert.Equal("event_2", document.AddEvent(1, 1).Id, "a new event does not take a locked id");
            ModAssert.Equal("rpg_slime_2", document.AddCharacter(0, 0, "rpg_slime", null, true).Id, "nor does a placed character");

            document.BeginChange();
            document.Events[0].Id = "EVENT_1";
            var problems = document.Validate();
            ModAssert.Equal(1, problems.Count, "renaming onto a locked id is a problem");
            ModAssert.Equal("tools/rpg_editor/problem_event_locked", problems[0].Key, "the problem says why");
            ModAssert.True(problems[0].Argument.Contains("quest_mod"), "and names the mod");

            document.Undo();
            ModAssert.Equal(2, document.LockedEvents.Count, "undo keeps the locked events");
            ModAssert.Equal("event_2", document.Events[0].Id, "and restores the own ones");
            ModAssert.False(document.EventsToJson().Contains("quest"), "locked events are not written");
        }

        [ModTest("document/resize_pulls_events_inside")]
        static void ResizePullsEventsInside(ModTestContext ctx)
        {
            var document = NewMap();
            var mapEvent = document.AddEvent(4, 3);
            document.Map.Spawn.X = 4;

            document.Resize(3, 2);

            ModAssert.Equal(2, mapEvent.X, "event x clamped");
            ModAssert.Equal(1, mapEvent.Y, "event y clamped");
            ModAssert.Equal(2, document.Map.Spawn.X, "spawn clamped");
        }

        [ModTest("document/validate_reports_ids")]
        static void ValidateReportsIds(ModTestContext ctx)
        {
            var document = NewMap();
            ModAssert.Equal(0, document.Validate().Count, "a fresh map is valid");

            document.AddEvent(0, 0).Id = "door";
            document.AddEvent(1, 0).Id = "DOOR";
            document.AddEvent(2, 0).Id = " ";

            var problems = document.Validate();
            ModAssert.Equal(2, problems.Count, "an empty id and a duplicate");
            ModAssert.Equal("tools/rpg_editor/problem_event_id", problems[0].Key, "empty id first");
            ModAssert.Equal("tools/rpg_editor/problem_event_duplicate", problems[1].Key, "duplicate ignores case");
        }

        [ModTest("document/saved_file_loads_back")]
        static void SavedFileLoadsBack(ModTestContext ctx)
        {
            var document = NewMap();
            document.Paint(RpgEditLayer.Main, 3, 2, 16, RpgCollision.Auto);
            document.Paint(RpgEditLayer.Collision, 0, 3, -1, RpgCollision.Open);
            var mapEvent = document.AddEvent(1, 2);
            mapEvent.Trigger = RpgEventTrigger.Touch;
            mapEvent.Teleport = new RpgTeleport { Map = "other", X = 4, Y = 5, Dir = RpgDirection.Left };

            var json = document.ToJson();
            var eventsJson = document.EventsToJson();
            var loaded = RpgMapDocument.Load(json, eventsJson);

            ModAssert.True(json.Contains("\"Kind\": \"rpg_map\""), "the file declares its kind");
            ModAssert.False(json.Contains("\"Events\""), "the map file holds no events");
            ModAssert.True(eventsJson.Contains("\"Events\""), "the events file holds them");
            ModAssert.False(eventsJson.Contains("IsSolid"), "the computed solidity is not written");
            ModAssert.True(json.EndsWith("}\n") && eventsJson.EndsWith("}\n"), "both files end with a line break");
            ModAssert.False(eventsJson.Contains("\"Scenario\""), "empty fields are left out");
            ModAssert.Equal("map_events/test_map/events.json", RpgMapDocument.EventsPath("test_map"), "the editor's events file of the map");
            ModAssert.Equal(0, RpgMapDocument.Load(json).Events.Count, "a map without an events file has no events");
            ModAssert.Equal("test_map", loaded.Map.ID, "id");
            ModAssert.Equal(16, loaded.Grid.GetTile(RpgLayer.Main, 3, 2), "main tile");
            ModAssert.Equal("o", loaded.Grid.GetCollision(0, 3).ToString(), "collision");
            ModAssert.Equal("Touch", loaded.Events[0].Trigger.ToString(), "trigger");
            ModAssert.Equal("other", loaded.Events[0].Teleport.Map, "teleport");
            ModAssert.Throws(() => RpgMapDocument.Load("{\"Kind\":\"rpg_map\",\"Items\":[]}"), "a file without maps", anyException: true);
        }
    }
}
