#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration smoke test for the map editor tool. The editor lives in the
    /// tools mod, which is compiled separately at runtime without the TOOLS
    /// define and without res:// script paths, so its types are reached by
    /// reflection like <see cref="SceneBuilderAutotest"/> does (a godot scene
    /// can only instantiate scripts from the core assembly). Checks the two
    /// things unit tests cannot:
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
        private const string WidgetTypeName = "Cthangover.Mods.Map.MapWidget";
        private const string ZonesLayerTypeName = "Cthangover.Mods.Map.MapZonesLayer";

        private int _passed;
        private int _failed;
        private bool _finished;
        private Window _window;

        /// <summary>
        /// Arms the 25-second watchdog and starts the async smoke-test sequence when
        /// the test scene loads.
        /// </summary>
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
                // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

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

            var region = controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
            var marker = controllerType.GetMethod("AddMarker").Invoke(controller, Array.Empty<object>());
            var label = controllerType.GetMethod("AddLabel").Invoke(controller, Array.Empty<object>());

            SetProp(region, "ID", "test_region");
            SetProp(region, "Title", "ui/map/test/region");
            SetProp(region, "Scene", "test_scene");
            SetProp(marker, "ID", "test_marker");
            SetProp(marker, "ImagePos", new Vector2(0.25f, 0.75f));
            SetProp(label, "ID", "test_label");
            SetProp(label, "LabelPos", new Vector2(0.6f, 0.4f));

            var json = (string)controllerType.GetMethod("ToJson").Invoke(controller, Array.Empty<object>());
            Check(json.Contains("\"Items\""), "json envelope present");
            Check(json.Contains("\"points\""), "region polygon serialised");
            Check(json.Contains("\"image_pos\""), "marker position serialised");
            Check(json.Contains("\"label_pos\""), "label position serialised");

            var restored = Activator.CreateInstance(controllerType);
            controllerType.GetMethod("FromJson").Invoke(restored, new object[] { json });

            var zones = GetProp(restored, "Zones") as IEnumerable;
            Check(zones != null && zones.Cast<object>().Count() == 3, "three zones restored");

            var restoredRegion = FindZone(zones, "test_region");
            var points = GetProp(restoredRegion, "Points") as IEnumerable;
            Check(restoredRegion != null && points != null && points.Cast<object>().Count() >= 3, "region polygon restored");

            var restoredMarker = FindZone(zones, "test_marker");
            Check(restoredMarker != null && (Vector2)GetProp(restoredMarker, "ImagePos") == new Vector2(0.25f, 0.75f), "marker position restored");

            var restoredLabel = FindZone(zones, "test_label");
            Check(restoredLabel != null && (Vector2)GetProp(restoredLabel, "LabelPos") == new Vector2(0.6f, 0.4f), "label position restored");
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
                // The window opens the first real map file on start; the checks
                // below count zones and labels, so they start from an empty map.
                controllerType.GetMethod("Clear").Invoke(controller, Array.Empty<object>());
                controllerType.GetMethod("AddMarker").Invoke(controller, Array.Empty<object>());

                canvas.Size = new Vector2(800f, 600f);
                canvas.GetType().GetMethod("RebuildHandles").Invoke(canvas, Array.Empty<object>());

                var handles = GetField(canvas.GetType(), canvas, "_handles") as ICollection;
                Check(handles is { Count: > 0 }, "fresh marker gets a draggable placeholder handle");

                var label = controllerType.GetMethod("AddLabel").Invoke(controller, Array.Empty<object>());
                SetProp(label, "ID", "autotest_label");
                SetProp(label, "Title", "ui/map/test/autotest_label");

                var hiddenLabel = controllerType.GetMethod("AddLabel").Invoke(controller, Array.Empty<object>());
                SetProp(hiddenLabel, "ID", "autotest_hidden_label");
                SetProp(hiddenLabel, "Title", "ui/map/test/autotest_hidden_label");
                SetProp(hiddenLabel, "Hidden", true);

                var region = controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
                SetProp(region, "ID", "autotest_region");
                SetProp(region, "Title", "ui/map/test/autotest_region");
                SetProp(region, "Scene", "home_outside");

                var lockedRegion = controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
                SetProp(lockedRegion, "ID", "autotest_locked_region");
                SetProp(lockedRegion, "Title", "ui/map/test/autotest_locked_region");
                SetProp(lockedRegion, "Scene", "home_outside");
                SetProp(lockedRegion, "Locked", true);

                var noSceneRegion = controllerType.GetMethod("AddRegion").Invoke(controller, Array.Empty<object>());
                SetProp(noSceneRegion, "ID", "autotest_no_scene_region");
                SetProp(noSceneRegion, "Title", "ui/map/test/autotest_no_scene_region");

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
            var widgetType = FindType(WidgetTypeName);
            var start = windowType.GetMethod("StartPreview");
            Check(start != null, "preview entry point present");
            if (start == null)
                return;

            var started = (bool)start.Invoke(_window, Array.Empty<object>());
            Check(started, "game-like preview starts");

            var preview = FindChildOf(_window, widgetType);
            Check(preview != null, "preview hosts the runtime map widget");

            if (preview != null)
            {
                var markers = preview.GetNodeOrNull<Control>("MapRoot/Sheet/MarkersLayer");
                Check(markers is { } && markers.GetChildCount() == 1, "preview renders only the visible zone label");

                var zonesLayer = preview.GetNodeOrNull("MapRoot/Sheet/ZonesLayer");
                var zonesLayerType = FindType(ZonesLayerTypeName);
                Check(zonesLayer != null && zonesLayerType != null && zonesLayerType.IsInstanceOfType(zonesLayer),
                    "preview hosts the interactive zones layer");
                if (zonesLayer != null && zonesLayerType != null && zonesLayerType.IsInstanceOfType(zonesLayer))
                {
                    var go = preview.GetNodeOrNull<Button>("ZoneCard/Column/GoButton");
                    var lockedNote = preview.GetNodeOrNull<Control>("ZoneCard/Column/Message");
                    // This engine build's C# signal generator emits PascalCase
                    // signal names (e.g. ZoneClicked, not zone_clicked), so
                    // resolve the generated constant instead of hardcoding a
                    // particular spelling.
                    var signalNameType = zonesLayerType.GetNestedType("SignalName");
                    object signalNameValue = signalNameType?.GetField("ZoneClicked")?.GetValue(null)
                        ?? signalNameType?.GetField("zone_clicked")?.GetValue(null);
                    var zoneClickedSignal = signalNameValue?.ToString() ?? "ZoneClicked";

                    zonesLayer.EmitSignal(zoneClickedSignal, "autotest_region");
                    var card = preview.GetNodeOrNull<Control>("ZoneCard");
                    Check(card is { Visible: true }, "clicking a region opens the location card");

                    var cardName = preview.GetNodeOrNull<Label>("ZoneCard/Column/Head/Info/Name");
                    Check(cardName is { } && cardName.Text == "ui/map/test/autotest_region",
                        "location card shows the edited zone title");
                    Check(go is { Disabled: false }, "open zone stays enterable in preview");
                    Check(lockedNote is { Visible: false }, "open zone shows no locked note");

                    zonesLayer.EmitSignal(zoneClickedSignal, "autotest_locked_region");
                    Check(go is { } && go.Disabled, "locked zone blocks the transition in preview");
                    Check(lockedNote is { Visible: true }, "locked zone shows the locked note in preview");

                    zonesLayer.EmitSignal(zoneClickedSignal, "autotest_no_scene_region");
                    Check(go is { } && go.Disabled, "zone without a scene stays blocked in preview");
                    Check(lockedNote is { Visible: true }, "zone without a scene shows the locked note in preview");
                }
            }

            var active = windowType.GetProperty("PreviewActive");
            Check(active != null && (bool)active.GetValue(_window), "preview reports active");

            windowType.GetMethod("StopPreview")?.Invoke(_window, Array.Empty<object>());
            await NextFrame();
            Check(FindChildOf(_window, widgetType) == null, "preview widget removed after stop");
        }

        private static void SetProp(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name);
            if (property == null)
                throw new MissingMemberException(target.GetType().Name, name);
            property.SetValue(target, value);
        }

        private static object GetProp(object target, string name)
        {
            var property = target.GetType().GetProperty(name);
            if (property == null)
                throw new MissingMemberException(target.GetType().Name, name);
            return property.GetValue(target);
        }

        private static object FindZone(IEnumerable zones, string id)
        {
            if (zones == null)
                return null;
            foreach (var zone in zones)
            {
                if (string.Equals((string)GetProp(zone, "ID"), id, StringComparison.Ordinal))
                    return zone;
            }
            return null;
        }

        private static Node FindChildOf(Node root, Type type)
        {
            if (root == null || type == null)
                return null;
            foreach (var child in root.GetChildren())
            {
                if (child != null && type.IsInstanceOfType(child))
                    return child;
            }
            return null;
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
