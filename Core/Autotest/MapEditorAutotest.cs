#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration smoke test for the map editor tool. The editor lives in the
    /// tools mod, which is compiled separately at runtime, so its types are
    /// reached by reflection like <see cref="SceneBuilderAutotest"/> does.
    /// Checks the two things unit tests cannot:
    /// <list type="number">
    /// <item>the authoring controller survives a JSON round-trip in a real
    /// process (region polygon, marker position, label position);</item>
    /// <item>the window constructs and enters the tree without touching Godot
    /// APIs incorrectly (custom chrome, draw callback, input subscriptions).</item>
    /// </list>
    /// Exit code 0 = pass.
    /// </summary>
    public partial class MapEditorAutotest : Node
    {
        private const string WindowTypeName = "Cthangover.Tools.MapEditor.MapEditorWindow";
        private const string ControllerTypeName = "Cthangover.Tools.MapEditor.MapEditorController";

        private int _passed;
        private int _failed;
        private bool _finished;
        private Window _window;

        public override void _Ready()
        {
            var watchdog = GetTree().CreateTimer(25d);
            watchdog.Timeout += OnWatchdogFired;
            Start();
        }

        private void OnWatchdogFired()
        {
            if (_finished)
                return;
            Fail("watchdog fired — aborting");
            Finish();
        }

        private async void Start()
        {
            try
            {
                if (!ModManager.Instance.IsInitialized)
                    ModManager.Instance.Initialize();

                var controllerType = FindType(ControllerTypeName);
                var windowType = FindType(WindowTypeName);
                if (controllerType == null || windowType == null)
                {
                    Fail("tools assembly not loaded (map editor types not found)");
                    Finish();
                    return;
                }

                CheckController(controllerType);
                await CheckWindow(windowType, controllerType);

                Finish();
            }
            catch (Exception ex)
            {
                Fail("exception: " + ex);
                Finish();
            }
        }

        /// <summary>
        /// Creates a region, a marker and a label through the controller's
        /// public API, serialises them, re-imports and verifies the structural
        /// properties survive the map file round-trip.
        /// </summary>
        private void CheckController(Type controllerType)
        {
            var controller = Activator.CreateInstance(controllerType);

            var region = (MapZone)controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
            var marker = (MapZone)controllerType.GetMethod("AddMarker").Invoke(controller, Array.Empty<object>());
            var label = (MapZone)controllerType.GetMethod("AddLabel").Invoke(controller, Array.Empty<object>());

            region.ID = "test_region";
            region.Title = "ui/map/test/region";
            region.Scene = "test_scene";
            marker.ID = "test_marker";
            marker.ImagePos = new Vector2(0.25f, 0.75f);
            label.ID = "test_label";
            label.LabelPos = new Vector2(0.6f, 0.4f);

            var json = (string)controllerType.GetMethod("ToJson").Invoke(controller, Array.Empty<object>());
            Check(json.Contains("\"Items\""), "json envelope present");
            Check(json.Contains("\"points\""), "region polygon serialised");
            Check(json.Contains("\"image_pos\""), "marker position serialised");
            Check(json.Contains("\"label_pos\""), "label position serialised");

            var restored = Activator.CreateInstance(controllerType);
            controllerType.GetMethod("FromJson").Invoke(restored, new object[] { json });

            var zones = (List<MapZone>)controllerType.GetProperty("Zones").GetValue(restored);
            Check(zones != null && zones.Count == 3, "three zones restored");

            var restoredRegion = zones?.FirstOrDefault(zone => zone.ID == "test_region");
            Check(restoredRegion != null && restoredRegion.Points.Count >= 3, "region polygon restored");

            var restoredMarker = zones?.FirstOrDefault(zone => zone.ID == "test_marker");
            Check(restoredMarker != null && restoredMarker.ImagePos == new Vector2(0.25f, 0.75f), "marker position restored");

            var restoredLabel = zones?.FirstOrDefault(zone => zone.ID == "test_label");
            Check(restoredLabel?.LabelPos == new Vector2(0.6f, 0.4f), "label position restored");
        }

        /// <summary>
        /// Constructs the editor window in a live Godot process and verifies its
        /// interactive preview exists after a frame. The window is built entirely
        /// from code (no tscn), so construction is the risk this covers. Also
        /// checks that a fresh icon-less marker gets a placeholder handle, so it
        /// stays visible and draggable before an icon is chosen.
        /// </summary>
        private async Task CheckWindow(Type windowType, Type controllerType)
        {
            _window = (Window)Activator.CreateInstance(windowType);
            await NextFrame();
            GetTree().Root.AddChild(_window);
            await NextFrame();

            var canvas = GetField(windowType, _window, "_canvas") as Control;
            Check(canvas is not null, "map editor canvas created");

            var jsonEdit = GetField(windowType, _window, "_jsonEdit") as TextEdit;
            Check(jsonEdit != null, "json preview created");

            if (canvas != null)
            {
                var controller = GetField(windowType, _window, "_controller");
                controllerType.GetMethod("AddMarker").Invoke(controller, Array.Empty<object>());

                canvas.Size = new Vector2(800f, 600f);
                canvas.GetType().GetMethod("RebuildHandles").Invoke(canvas, Array.Empty<object>());

                var handles = GetField(canvas.GetType(), canvas, "_handles") as System.Collections.ICollection;
                Check(handles is { Count: > 0 }, "fresh marker gets a draggable placeholder handle");

                var label = (MapZone)controllerType.GetMethod("AddLabel").Invoke(controller, Array.Empty<object>());
                label.ID = "autotest_label";
                label.Title = "ui/map/test/autotest_label";

                var region = (MapZone)controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
                region.ID = "autotest_region";
                region.Title = "ui/map/test/autotest_region";
                region.Scene = "test_scene";

                await CheckPreview(windowType);
            }

            _window.QueueFree();
            _window = null;
        }

        /// <summary>
        /// Exercises the game-like preview: the editor must be able to host the
        /// runtime map widget fed with the edited zones, render a label from
        /// that data, report the preview state, and give the editor back when
        /// the preview stops.
        /// </summary>
        private async Task CheckPreview(Type windowType)
        {
            var start = windowType.GetMethod("StartPreview");
            Check(start != null, "preview entry point present");
            if (start == null)
                return;

            var started = (bool)start.Invoke(_window, Array.Empty<object>());
            Check(started, "game-like preview starts");

            var preview = _window.GetChildren().OfType<MapWidget>().FirstOrDefault();
            Check(preview != null, "preview hosts the runtime map widget");

            if (preview != null)
            {
                var markers = preview.GetNodeOrNull<Control>("MapRoot/Sheet/MarkersLayer");
                Check(markers is { } && markers.GetChildCount() > 0, "preview renders the edited zone label");

                var zonesLayer = preview.GetNodeOrNull<MapZonesLayer>("MapRoot/Sheet/ZonesLayer");
                Check(zonesLayer != null, "preview hosts the interactive zones layer");
                if (zonesLayer != null)
                {
                    zonesLayer.EmitSignal(MapZonesLayer.SignalName.ZoneClicked, "autotest_region");
                    var card = preview.GetNodeOrNull<Control>("ZoneCard");
                    Check(card is { Visible: true }, "clicking a region opens the location card");

                    var cardName = preview.GetNodeOrNull<Label>("ZoneCard/Column/Head/Info/Name");
                    Check(cardName is { } && cardName.Text == "ui/map/test/autotest_region",
                        "location card shows the edited zone title");
                }
            }

            var active = windowType.GetProperty("PreviewActive");
            Check(active != null && (bool)active.GetValue(_window), "preview reports active");

            windowType.GetMethod("StopPreview")?.Invoke(_window, Array.Empty<object>());
            await NextFrame();
            Check(!_window.GetChildren().OfType<MapWidget>().Any(), "preview widget removed after stop");
        }

        private Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static object GetField(Type startType, object instance, string name)
        {
            var type = startType;
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(instance);
                type = type.BaseType;
            }
            throw new MissingFieldException(startType.Name, name);
        }

        private async Task NextFrame()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        private void Check(bool condition, string label)
        {
            if (condition)
                Pass(label);
            else
                Fail(label);
        }

        private void Pass(string label)
        {
            _passed++;
            GameLogger.Log("MAP_EDITOR_TEST", $"  [PASS] {label}");
        }

        private void Fail(string message)
        {
            _failed++;
            GameLogger.Log("MAP_EDITOR_TEST", $"  [FAIL] {message}", LogLevel.Error);
        }

        private void Finish()
        {
            if (_finished)
                return;
            _finished = true;

            if (_window != null && GodotObject.IsInstanceValid(_window))
                _window.QueueFree();

            GameLogger.Log("MAP_EDITOR_TEST", $"MapEditorAutotest: {_passed} passed, {_failed} failed");
            GameLogger.Log("MAP_EDITOR_TEST", $"[TEST_RESULT] name=map_editor passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }
    }
}
#endif
