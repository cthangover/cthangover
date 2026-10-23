using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Rpg;
using Cthangover.Tools.Common;
using Cthangover.Tools.Services;
using Cthangover.Mods.Interface.View;
using Godot;

namespace Cthangover.Tools.RpgEditor.Tests
{
    /// <summary>
    /// Drives the RPG map editor through its controls like an author: layer
    /// buttons are pressed, the palette and the canvas get mouse events through
    /// their <c>gui_input</c> signal, Ctrl+Z goes into the window, a slime found in
    /// the characters tab is placed as an enemy and branches from the items tab as
    /// a pickup. That window is
    /// freed without saving, so the demo maps never change. A second window then
    /// creates a new map through the file selector's "+" and saves it: the game must
    /// know the map's scene and its event (saved apart, into map_events) at once,
    /// with no mod reload; its files are deleted again.
    /// Result line: <c>[TEST_RESULT] name=rpg_editor_edit passed=N failed=M</c>.
    /// </summary>
    public partial class RpgEditorTestDriver : Node
    {
        private const float TickSeconds = 0.15f;
        private const float TimeoutSeconds = 60f;
        private const int RockTile = 11;
        private const string NewMap = "zz_editor_test_map";

        private static readonly Vector2I PaintCell = new(3, 5);
        private static readonly Vector2I EventCell = new(2, 6);
        private static readonly Vector2I CharacterCell = new(4, 6);
        private static readonly Vector2I ItemCell = new(5, 6);

        private readonly string shotsDir;
        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private double elapsed;
        private RpgMapEditorWindow window;
        private RpgMapEditorCanvas canvas;
        private int eventsBefore;
        private string newMapMod;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> null disables screenshots.</summary>
        public RpgEditorTestDriver(string shotsDir)
        {
            this.shotsDir = shotsDir;
            Name = "RpgEditorTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step}");
                Finish();
                return;
            }
            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady || RpgWorldHost.Current?.View == null)
                        return;
                    var box = RpgInput.FindDialogBox();
                    if (box != null && box.IsRunning)
                    {
                        box.NextAction();
                        return;
                    }
                    window = ToolWindow.Open<RpgMapEditorWindow>();
                    step++;
                    return;
                case 1:
                    canvas = window.FindChildren("*", "", true, false).OfType<RpgMapEditorCanvas>().FirstOrDefault();
                    if (canvas?.Document == null || canvas.Tileset == null)
                        return;
                    Check(canvas.Document.Map.ID.StartsWith("rpg_"), $"the editor opened a demo map ({canvas.Document.Map.ID})");
                    Check(canvas.Document.Events.Count > 0 && canvas.Document.LockedEvents.Count == 0,
                        $"its events come from the mod's map_events file ({canvas.Document.Events.Count} own, {canvas.Document.LockedEvents.Count} locked)");
                    Shot("editor");
                    Press("tools/rpg_editor/layer_main");
                    Check(canvas.ActiveLayer == RpgEditLayer.Main, "the layer button switches the brush to the main layer");
                    ClickPalette(RockTile);
                    Check(canvas.SelectedTile == RockTile, $"a palette click picks tile {RockTile}, got {canvas.SelectedTile}");
                    step++;
                    return;
                case 2:
                    var before = canvas.Document.Grid.GetTile(RpgLayer.Main, PaintCell.X, PaintCell.Y);
                    ClickCanvas(PaintCell, false);
                    Check(canvas.Document.Grid.GetTile(RpgLayer.Main, PaintCell.X, PaintCell.Y) == RockTile, "a canvas click paints the tile");
                    Check(window.Title.StartsWith("* "), "the window is marked dirty");
                    window.PushInput(new InputEventKey { Keycode = Key.Z, PhysicalKeycode = Key.Z, CtrlPressed = true, Pressed = true });
                    Check(canvas.Document.Grid.GetTile(RpgLayer.Main, PaintCell.X, PaintCell.Y) == before, "Ctrl+Z undoes the stroke");
                    Press("tools/rpg_editor/layer_events");
                    eventsBefore = canvas.Document.Events.Count;
                    ClickCanvas(EventCell, true);
                    Check(canvas.Document.Events.Count == eventsBefore + 1 && canvas.SelectedEvent != null, "a double click adds and selects an event");
                    step++;
                    return;
                case 3:
                    // Screenshots one tick after the action, when the frame shows it.
                    Shot("events");
                    PlaceSlime();
                    Press("tools/rpg_editor/layer_collision");
                    step++;
                    return;
                case 4:
                    Shot("collision");
                    window.QueueFree();
                    window = ToolWindow.Open<RpgMapEditorWindow>();
                    step++;
                    return;
                case 5:
                    canvas = window.FindChildren("*", "", true, false).OfType<RpgMapEditorCanvas>().FirstOrDefault();
                    if (canvas?.Document == null)
                        return;
                    newMapMod = Selector().CurrentMod;
                    DeleteNewMapFiles();
                    Selector().FindChildren("*", "", true, false).OfType<Button>().First(b => b.Text == "+").EmitSignal(BaseButton.SignalName.Pressed);
                    step++;
                    return;
                case 6:
                    var dialog = window.FindChildren("*", "", true, false).OfType<ConfirmationDialog>().FirstOrDefault();
                    if (dialog == null)
                        return;
                    dialog.FindChildren("*", "", true, false).OfType<LineEdit>().First().Text = NewMap;
                    dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                    step++;
                    return;
                case 7:
                    if (canvas.Document?.Map?.ID != NewMap)
                        return;
                    try
                    {
                        canvas.Document.AddEvent(1, 1).Sprite = "village/elder";
                        Press("tools/common/save");
                        Check(File.Exists(ModFileService.GetWritablePath(newMapMod, "scenes/" + NewMap + ".json")), "the first save of a new map writes its scene");
                        Check(!ModFileService.ReadText(newMapMod, "maps/" + NewMap + ".json").Contains("\"Events\""), "the map file holds no events");
                        Check(RpgMapEvents.Of(NewMap).Any(e => e.Id == "event_1" && e.ModId == newMapMod && e.File == RpgMapDocument.EventsPath(NewMap)),
                            "the event is saved into map_events and the game reads it at once");
                        Check(GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager")?.HasScene(NewMap) == true, "the game knows the new map's scene without a mod reload");
                    }
                    finally
                    {
                        window.QueueFree();
                        DeleteNewMapFiles();
                        SceneManager.ReloadContent();
                    }
                    Finish();
                    return;
            }
        }

        /// <summary>Characters tab: search "slime", pick it, "place as enemy", click a cell; items tab: two bundles of branches.</summary>
        private void PlaceSlime()
        {
            var picker = Pick("Characters", 1, "slime", "rpg_slime");
            if (picker == null)
                return;
            Check(picker.Selected.Look == "village/slime" && picker.Selected.Icon != null, $"the slime is picked with its charset ({picker.Selected.Look})");

            var count = canvas.Document.Events.Count;
            Press("tools/rpg_editor/place_enemy");
            Check(canvas.Placing && canvas.ActiveLayer == RpgEditLayer.Events, "placing waits for a cell on the events layer");
            ClickCanvas(CharacterCell, false);
            var placed = canvas.SelectedEvent;
            Check(canvas.Document.Events.Count == count + 1 && placed?.Sprite == "village/slime" && placed.X == CharacterCell.X && placed.Y == CharacterCell.Y,
                "a click on the map puts the slime there");
            Check(placed?.Commands?.FirstOrDefault()?.StartsWith("action rpg.battle enemies=rpg_slime ") == true && !canvas.Placing, "the placed enemy starts a battle with it");

            picker = Pick("Items", 2, "branches", "rpg_demo/branches");
            if (picker == null)
                return;
            Check(picker.Selected.Look == "rpg_demo/branch_bundle" && picker.Selected.Icon != null, $"the branches are picked with their picture ({picker.Selected.Look})");
            var countSpin = picker.FindChildren("*", "", true, false).OfType<SpinBox>().First();
            countSpin.Value = 2;
            Press("tools/rpg_editor/place_item");
            ClickCanvas(ItemCell, false);
            placed = canvas.SelectedEvent;
            Check(placed?.Icon == "rpg_demo/branch_bundle" && placed.X == ItemCell.X && placed.Trigger == RpgEventTrigger.Touch && placed.Solid == false,
                "a click on the map lays the branches there, picked up by stepping on them");
            Check(placed?.Commands?.Contains("action item.add id=rpg_demo/branches count=2") == true, "the pickup gives the chosen count");
        }

        /// <summary>Opens the side tab, types the query, checks every row matches it and selects <paramref name="id"/>; null when it is not listed.</summary>
        private RpgEntityPicker Pick(string name, int tab, string query, string id)
        {
            window.FindChildren("*", "", true, false).OfType<TabContainer>().First(t => t.GetTabCount() == 3).CurrentTab = tab;
            var picker = window.FindChildren("*", "", true, false).OfType<RpgEntityPicker>().First(p => p.Name == name);
            var search = picker.FindChildren("*", "", true, false).OfType<LineEdit>().First(e => e.GetParent() == picker);
            search.Text = query;
            search.EmitSignal(LineEdit.SignalName.TextChanged, query);
            var list = picker.FindChildren("*", "", true, false).OfType<ItemList>().First();
            Check(list.ItemCount >= 1 && Enumerable.Range(0, list.ItemCount).All(i => list.GetItemText(i).Contains(query, System.StringComparison.OrdinalIgnoreCase)),
                $"the {name} search keeps only '{query}' ({list.ItemCount} rows)");
            for (var i = 0; i < list.ItemCount; i++)
            {
                if (!list.GetItemText(i).EndsWith("·  " + id))
                    continue;
                list.Select(i);
                list.EmitSignal(ItemList.SignalName.ItemSelected, i);
                return picker;
            }
            Fail($"'{id}' is not in the {name} list");
            return null;
        }

        private ModFileSelector Selector() => window.FindChildren("*", "", true, false).OfType<ModFileSelector>().First();

        private void DeleteNewMapFiles()
        {
            foreach (var file in new[] { "maps/" + NewMap + ".json", "scenes/" + NewMap + ".json", RpgMapDocument.EventsPath(NewMap) })
            {
                var path = ModFileService.GetWritablePath(newMapMod, file);
                if (path != null && File.Exists(path))
                    File.Delete(path);
            }
            var eventsDir = Path.GetDirectoryName(ModFileService.GetWritablePath(newMapMod, RpgMapDocument.EventsPath(NewMap)) ?? "");
            if (!string.IsNullOrEmpty(eventsDir) && Directory.Exists(eventsDir) && !Directory.EnumerateFileSystemEntries(eventsDir).Any())
                Directory.Delete(eventsDir);
            RpgMapEvents.Invalidate();
        }

        private void Press(string key)
        {
            var text = TranslationServer.Translate(key);
            var button = window.FindChildren("*", "", true, false).OfType<Button>().FirstOrDefault(b => b.Text == text);
            if (button == null)
            {
                Fail($"button '{text}' not found");
                return;
            }
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void ClickPalette(int index)
        {
            var palette = window.FindChildren("*", "", true, false).OfType<RpgTilePalette>().First();
            var tileset = canvas.Tileset;
            var cell = palette.Size.X / tileset.Columns;
            var position = new Vector2(index % tileset.Columns + 0.5f, index / tileset.Columns + 0.5f) * cell;
            palette.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position });
        }

        private void ClickCanvas(Vector2I cell, bool doubleClick)
        {
            var size = canvas.Tileset.TileSize * canvas.Zoom;
            var position = new Vector2(cell.X + 0.5f, cell.Y + 0.5f) * size;
            canvas.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, DoubleClick = doubleClick, Position = position });
            canvas.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position });
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                passed++;
                GameLogger.Log("TEST", $"[PASS] {what}");
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            failures.Add(what);
            GameLogger.Log("TEST", $"[FAIL] {what}", LogLevel.Error);
        }

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shotsDir) || DisplayServer.GetName() == "headless")
                return;
            DirAccess.MakeDirRecursiveAbsolute(shotsDir);
            ViewportCapture.SavePng(GetViewport(), shotsDir.TrimEnd('/') + "/rpg_editor_" + name + ".png");
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_editor_edit passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
