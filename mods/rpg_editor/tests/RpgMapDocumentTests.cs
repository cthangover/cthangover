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
            ModAssert.Equal(0, document.Map.Events.Count, "event gone");
            ModAssert.Equal(9, document.Grid.GetTile(RpgLayer.Front, 0, 0), "the earlier paint stays");
            ModAssert.True(document.Undo(), "undo the paint");
            ModAssert.Equal(-1, document.Grid.GetTile(RpgLayer.Front, 0, 0), "paint gone");
            ModAssert.False(document.Undo(), "nothing more");

            ModAssert.True(document.Redo(), "redo the paint");
            ModAssert.True(document.Redo(), "redo the event");
            ModAssert.Equal("village/elder", document.Map.Events[0].Sprite, "event back with its data");

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
            var loaded = RpgMapDocument.Load(json);

            ModAssert.True(json.Contains("\"Kind\": \"rpg_map\""), "the file declares its kind");
            ModAssert.False(json.Contains("\"Scenario\""), "empty fields are left out");
            ModAssert.Equal("test_map", loaded.Map.ID, "id");
            ModAssert.Equal(16, loaded.Grid.GetTile(RpgLayer.Main, 3, 2), "main tile");
            ModAssert.Equal("o", loaded.Grid.GetCollision(0, 3).ToString(), "collision");
            ModAssert.Equal("Touch", loaded.Map.Events[0].Trigger.ToString(), "trigger");
            ModAssert.Equal("other", loaded.Map.Events[0].Teleport.Map, "teleport");
            ModAssert.Throws(() => RpgMapDocument.Load("{\"Kind\":\"rpg_map\",\"Items\":[]}"), "a file without maps", anyException: true);
        }
    }
}
