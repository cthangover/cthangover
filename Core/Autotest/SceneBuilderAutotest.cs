#if TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration check for the Scene Builder tool chain, run inside a real
    /// game process because most of the pipeline (mod assembly events, Timer
    /// signals, Variant marshaling) cannot execute under plain unit tests.
    /// Via reflection it instantiates the tools-mod SceneBuilderWindow, opens
    /// cooking/cooking_panel.tscn through the real dropdowns, pushes an
    /// inspector edit through the private write-back handler, waits out the
    /// debounced auto-save and asserts the change reached the file on disk,
    /// then restores the original file content. Exit code 0 = pass.
    /// Launch: godot --path . scenes/test/scene_builder_autotest.tscn
    /// </summary>
    public partial class SceneBuilderAutotest : Node
    {
        private const string TargetModId = "cooking";
        // Dropdown labels carry the file name incl. extension (Path.GetFileName).
        private const string TargetSceneName = "cooking_panel.tscn";
        private const string TargetSceneResPath = "res://mods/cooking/scenes/cooking_panel.tscn";
        private const string WindowTypeName = "Cthangover.Core.UI.Tool.SceneBuilder.SceneBuilderWindow";
        private const string ProbeKey = "anchor_right";
        private const float ProbeValue = 0.75f;

        private int _failures;
        private bool _finished;
        private string _originalText;
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
            GameLogger.Log("SCENE_BUILDER_TEST", "watchdog fired — aborting", LogLevel.Error);
            Finish();
        }

        private async void Start()
        {
            try
            {
                if (!PrepareOriginalFile())
                {
                    Finish();
                    return;
                }

                var windowType = FindWindowType();
                if (windowType == null)
                {
                    Fail("tools assembly not loaded (window type not found)");
                    Finish();
                    return;
                }

                _window = (Window)Activator.CreateInstance(windowType);
                // Root is busy inside its own child setup during _Ready — defer.
                await NextFrame();
                GetTree().Root.AddChild(_window);
                await NextFrame();

                if (!await SelectSceneInDropdowns(windowType))
                {
                    Finish();
                    return;
                }

                var node = FindDescendantByName(GetModRoot(windowType), "BgImage");
                if (node == null)
                {
                    Fail("preview node 'BgImage' not found");
                    Finish();
                    return;
                }

                InvokeCommit(windowType, node);
                var editor = (TextEdit)GetField(windowType, _window, "_tscnEditor");
                var editorText = editor.Text;
                Check(editorText != null && editorText.Contains(ProbeKey + " = " + ProbeLiteral()),
                    $"editor text patched with '{ProbeKey} = {ProbeLiteral()}'");

                InvokeBatch(windowType, node);

                await WaitSeconds(1.0d);
                var onDisk = File.ReadAllText(ProjectSettings.GlobalizePath(TargetSceneResPath));
                Check(onDisk.Contains(ProbeKey + " = " + ProbeLiteral()), "auto-save wrote inspector probe to disk");
                Check(onDisk.Contains("offset_left = -5.0") && onDisk.Contains("offset_right = 5.0"),
                    "auto-save wrote overlay batch to disk");

                // 4) node creation through the real UI handler (no selection → parent is scene root)
                InvokeOnWindow(windowType, _window, "OnAddNodePressed");
                var added = FindDescendantByName(GetModRoot(windowType), "Control");
                Check(added != null, "live node 'Control' created under scene root");
                var textAfterAdd = ((TextEdit)GetField(windowType, _window, "_tscnEditor")).Text;
                Check(textAfterAdd.Contains("[node name=\"Control\" type=\"Control\" parent=\".\"]"),
                    "tscn section inserted for new node");

                await WaitSeconds(1.0d);
                var diskAfterAdd = File.ReadAllText(ProjectSettings.GlobalizePath(TargetSceneResPath));
                Check(diskAfterAdd.Contains("[node name=\"Control\" type=\"Control\" parent=\".\"]"),
                    "auto-save wrote new node section to disk");

                // 5) deletion of that node
                SetField(windowType, _window, "_selectedNode", added);
                InvokeOnWindow(windowType, _window, "OnDeleteNodePressed");
                Check(FindDescendantByName(GetModRoot(windowType), "Control") == null, "live node removed from preview");
                var textAfterDelete = ((TextEdit)GetField(windowType, _window, "_tscnEditor")).Text;
                Check(!textAfterDelete.Contains("[node name=\"Control\" type=\"Control\" parent=\".\"]"),
                    "tscn section removed from editor text");

                await WaitSeconds(1.0d);
                var diskAfterDelete = File.ReadAllText(ProjectSettings.GlobalizePath(TargetSceneResPath));
                Check(!diskAfterDelete.Contains("[node name=\"Control\" type=\"Control\" parent=\".\"]"),
                    "auto-save persisted the deletion to disk");

                Finish();
            }
            catch (Exception ex)
            {
                Fail("exception: " + ex);
                Finish();
            }
        }

        private bool PrepareOriginalFile()
        {
            _originalText = File.ReadAllText(ProjectSettings.GlobalizePath(TargetSceneResPath));
            if (_originalText.Contains(ProbeKey + " = " + ProbeLiteral()))
            {
                Fail($"file already contains probe value '{ProbeLiteral()}' — pick another key");
                return false;
            }
            return true;
        }

        // Mirrors TscnValueFormatter.FormatFloat for the probe value: shortest
        // float round-trip plus a guaranteed decimal point.
        private static string ProbeLiteral()
        {
            var s = ProbeValue.ToString("R", CultureInfo.InvariantCulture);
            if (!s.Contains('.'))
                s += ".0";
            return s;
        }

        private Type FindWindowType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(WindowTypeName);
                if (t != null)
                    return t;
            }
            return null;
        }

        private async Task<bool> SelectSceneInDropdowns(Type windowType)
        {
            var modDropdown = (OptionButton)GetField(windowType, _window, "_modDropdown");
            var sceneDropdown = (OptionButton)GetField(windowType, _window, "_sceneDropdown");

            var modIdx = FindItem(modDropdown, TargetModId);
            if (modIdx < 0)
            {
                Fail($"mod '{TargetModId}' missing from dropdown");
                return false;
            }
            modDropdown.Selected = modIdx;
            modDropdown.EmitSignal(OptionButton.SignalName.ItemSelected, (long)modIdx);
            await NextFrame();

            var sceneIdx = FindItem(sceneDropdown, TargetSceneName);
            if (sceneIdx < 0)
            {
                Fail($"scene '{TargetSceneName}' missing from dropdown");
                return false;
            }
            sceneDropdown.Selected = sceneIdx;
            sceneDropdown.EmitSignal(OptionButton.SignalName.ItemSelected, (long)sceneIdx);
            await NextFrame();
            return true;
        }

        private Node GetModRoot(Type windowType)
        {
            var controller = GetField<object>(windowType, _window, "_controller");
            return GetField<Node>(controller.GetType(), controller, "_modRoot");
        }

        private void InvokeCommit(Type windowType, Node node)
        {
            var mi = windowType.GetMethod("OnInspectorPropertyEdited", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(_window, new object[] { node, ProbeKey, Variant.From(ProbeValue) });
        }

        private static void InvokeOnWindow(Type type, object instance, string methodName)
        {
            var mi = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null)
                throw new MissingMethodException(type.Name, methodName);
            mi.Invoke(instance, Array.Empty<object>());
        }

        private static void SetField(Type type, object instance, string name, object value)
        {
            var current = type;
            while (current != null)
            {
                var fi = current.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null)
                {
                    fi.SetValue(instance, value);
                    return;
                }
                current = current.BaseType;
            }
            throw new MissingFieldException(type.Name, name);
        }

        private void InvokeBatch(Type windowType, Node node)
        {
            var mi = windowType.GetMethod("OnOverlayBatchCommitted", BindingFlags.NonPublic | BindingFlags.Instance);
            var batch = new List<KeyValuePair<string, Variant>>
            {
                new KeyValuePair<string, Variant>("layout_mode", Variant.From(1L)),
                new KeyValuePair<string, Variant>("offset_left", Variant.From(-5f)),
                new KeyValuePair<string, Variant>("offset_right", Variant.From(5f))
            };
            mi.Invoke(_window, new object[] { node, batch });
        }

        private static int FindItem(OptionButton dropdown, string text)
        {
            for (var i = 0; i < dropdown.ItemCount; i++)
            {
                if (dropdown.GetItemText(i) == text)
                    return i;
            }
            return -1;
        }

        private static Node FindDescendantByName(Node root, string name)
        {
            if (root == null)
                return null;
            if (root.Name == name)
                return root;
            foreach (var child in root.GetChildren())
            {
                var found = FindDescendantByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static object GetField(Type startType, object instance, string name)
        {
            var type = startType;
            while (type != null)
            {
                var fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null)
                    return fi.GetValue(instance);
                type = type.BaseType;
            }
            throw new MissingFieldException(startType.Name, name);
        }

        private static T GetField<T>(Type startType, object instance, string name)
        {
            return (T)GetField(startType, instance, name);
        }

        private async Task NextFrame()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        private async Task WaitSeconds(double seconds)
        {
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        }

        private void Check(bool condition, string label)
        {
            if (condition)
                GameLogger.Log("SCENE_BUILDER_TEST", $"PASS {label}");
            else
            {
                _failures++;
                GameLogger.Log("SCENE_BUILDER_TEST", $"FAIL {label}", LogLevel.Error);
            }
        }

        private void Fail(string message)
        {
            _failures++;
            GameLogger.Log("SCENE_BUILDER_TEST", $"FAIL {message}", LogLevel.Error);
        }

        private void Finish()
        {
            if (_finished)
                return;
            _finished = true;

            // Stop the debounced auto-save before restoring, otherwise it may
            // rewrite edited content over the original file after we exit.
            if (_window != null && GodotObject.IsInstanceValid(_window))
            {
                var timer = GetField(_window.GetType(), _window, "_autosaveTimer") as Timer;
                timer?.Stop();
            }

            if (_originalText != null && File.Exists(ProjectSettings.GlobalizePath(TargetSceneResPath)))
                File.WriteAllText(ProjectSettings.GlobalizePath(TargetSceneResPath), _originalText);

            GameLogger.Log("SCENE_BUILDER_TEST",
                _failures == 0 ? "RESULT: ALL PASSED" : $"RESULT: {_failures} FAILURES",
                _failures == 0 ? LogLevel.Message : LogLevel.Error);
            GetTree().Quit(_failures == 0 ? 0 : 1);
        }
    }
}
#endif
