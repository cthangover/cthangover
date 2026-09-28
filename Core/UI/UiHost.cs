using System;
using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Persistent autoload that owns the mod-built GUI for the whole session.
    /// Panels live under its own <see cref="CanvasLayer"/> instead of the base
    /// scene, so entering a separate native or mod-routed scene (e.g. battle)
    /// and coming back does not rebuild them — the registry only re-attaches
    /// and re-shows what is already cached.
    /// <para/>
    /// Responsibilities: instance cache keyed by <see cref="UiEntry.Id"/>, lazy
    /// construction through <see cref="TscnScenes"/>, scene-based visibility
    /// (entries bound to a logical scene hide on exit and restore on re-entry),
    /// and tree access for mod code (<see cref="FindNode"/>, <see cref="Join"/>).
    /// It subscribes to <see cref="ShellReadyEvent"/> to learn the current shell
    /// root and to trigger <see cref="UiBuilder.Build"/>.
    /// </summary>
    public partial class UiHost : Node
    {
        /// <summary>Autoload singleton; null outside runtime.</summary>
        public static UiHost Instance { get; private set; }

        private CanvasLayer layer;
        private Node shellRoot;
        private readonly Dictionary<string, Control> instances = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> toggled = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hiddenByScene = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Root of the currently loaded shell, or null before the first ShellReadyEvent.</summary>
        public Node ShellRoot => shellRoot;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            layer = new CanvasLayer { Name = "UiHostLayer", Layer = 100 };
            AddChild(layer);

            ModEventBus.Subscribe<ShellReadyEvent>(OnShellReady);
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered, priority: 30);
            ModEventBus.Subscribe<SceneExitedEvent>(OnSceneExited, priority: 30);
        }

        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Finds a node by name in the current shell. Backs both the registry
        /// anchoring and mod snippets that need to reach into the shell tree.
        /// </summary>
        public Node FindNode(string name)
        {
            if (string.IsNullOrEmpty(name) || shellRoot == null || !GodotObject.IsInstanceValid(shellRoot))
                return null;
            return shellRoot.FindChild(name, true, false);
        }

        /// <summary>
        /// Parents an already built control to an arbitrary existing node, for mod
        /// code that wants its element to be part of that node's layout instead of a
        /// hosted overlay. Joined elements are freed together with their parent and
        /// therefore lose the app-lifetime guarantee panels normally get.
        /// </summary>
        public bool Join(Node parent, Control panel)
        {
            if (parent == null || panel == null)
                return false;
            parent.AddChild(panel);
            return true;
        }

        /// <summary>
        /// Shows or hides the entry with the given id, creating its instance on
        /// first use. Panels are toggled exclusively through this method.
        /// </summary>
        public void Toggle(string id)
        {
            var entry = UiRegistry.Get(id);
            if (entry == null)
            {
                GameLogger.Log("UI_HOST", $"Toggle('{id}'): entry not found", LogLevel.Warning);
                return;
            }

            var instance = EnsureInstance(entry);
            if (instance == null)
                return;

            hiddenByScene.Remove(id);
            if (instance is Widget widget)
            {
                widget.Switch();
                toggled[id] = widget.Visible;
            }
            else
            {
                var visible = !instance.Visible;
                instance.Visible = visible;
                toggled[id] = visible;
            }
        }

        /// <summary>
        /// Returns the cached instance for an entry, building it on first use.
        /// Hosted entries (panels and once-overlays) are parented to the host layer;
        /// transient overlays are joined to their slot in the current shell.
        /// </summary>
        public Control EnsureInstance(UiEntry entry)
        {
            if (instances.TryGetValue(entry.Id, out var cached))
            {
                if (GodotObject.IsInstanceValid(cached))
                    return cached;
                instances.Remove(entry.Id);
            }

            var node = TscnScenes.LoadAndBuild(entry.ScenePath);
            if (node is not Control control)
            {
                GameLogger.Log("UI_HOST", $"EnsureInstance('{entry.Id}'): '{entry.ScenePath}' did not build a Control", LogLevel.Error);
                return null;
            }

            control.Name = entry.Id;
            CthangoverTheme.Apply(control);
            if (IsHosted(entry))
            {
                layer.AddChild(control);
                control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            else
            {
                var slot = FindNode(entry.Slot);
                if (slot == null)
                {
                    GameLogger.Log("UI_HOST", $"EnsureInstance('{entry.Id}'): slot '{entry.Slot}' not found", LogLevel.Warning);
                    control.QueueFree();
                    return null;
                }
                slot.AddChild(control);
                control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }

            if (control is ModWidget modWidget)
                modWidget.EnsureConstructed();

            instances[entry.Id] = control;
            SetVisible(control, false);
            return control;
        }

        /// <summary>
        /// Frees every cached instance and forgets the shell. Called by
        /// <see cref="ModManager"/> so a hot-reloaded mod set starts clean.
        /// </summary>
        public void ClearAll()
        {
            foreach (var instance in instances.Values)
            {
                if (GodotObject.IsInstanceValid(instance))
                    instance.QueueFree();
            }
            instances.Clear();
            toggled.Clear();
            hiddenByScene.Clear();
            shellRoot = null;
        }

        private static bool IsHosted(UiEntry entry)
            => UiRegistry.IsPanel(entry) || entry.Once;

        private void OnShellReady(ShellReadyEvent e)
        {
            PurgeInvalidInstances();
            shellRoot = e.Root;
            CthangoverTheme.Apply(e.Root);
            UiBuilder.Build(e.Root);

            foreach (var entry in UiRegistry.Entries)
            {
                if (IsSceneBound(entry) || UiRegistry.IsPanel(entry) || !entry.ShowOnStart)
                    continue;
                toggled[entry.Id] = true;
                var instance = EnsureInstance(entry);
                SetVisible(instance, true);
            }
        }

        private void OnSceneEntered(SceneEnteredEvent e)
        {
            foreach (var entry in UiRegistry.Entries)
            {
                if (!IsSceneBound(entry) || !string.Equals(entry.Scene, e.SceneName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!UiRegistry.IsPanel(entry) && entry.ShowOnStart)
                {
                    toggled[entry.Id] = true;
                    hiddenByScene.Remove(entry.Id);
                    SetVisible(EnsureInstance(entry), true);
                    continue;
                }

                if (hiddenByScene.Remove(entry.Id) && toggled.GetValueOrDefault(entry.Id))
                    SetVisible(EnsureInstance(entry), true);
            }
        }

        private void OnSceneExited(SceneExitedEvent e)
        {
            foreach (var entry in UiRegistry.Entries)
            {
                if (!IsSceneBound(entry) || !string.Equals(entry.Scene, e.SceneName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!entry.Once)
                {
                    DestroyInstance(entry.Id);
                    continue;
                }

                if (toggled.GetValueOrDefault(entry.Id))
                {
                    hiddenByScene.Add(entry.Id);
                    if (instances.TryGetValue(entry.Id, out var instance))
                        SetVisible(instance, false);
                }
            }
        }

        private void PurgeInvalidInstances()
        {
            var stale = new List<string>();
            foreach (var pair in instances)
            {
                if (!GodotObject.IsInstanceValid(pair.Value))
                    stale.Add(pair.Key);
            }

            foreach (var id in stale)
            {
                instances.Remove(id);
                toggled.Remove(id);
                hiddenByScene.Remove(id);
            }
        }

        private void DestroyInstance(string id)
        {
            if (instances.TryGetValue(id, out var instance))
            {
                if (GodotObject.IsInstanceValid(instance))
                    instance.QueueFree();
                instances.Remove(id);
            }
            toggled.Remove(id);
            hiddenByScene.Remove(id);
        }

        private static bool IsSceneBound(UiEntry entry)
            => !string.IsNullOrEmpty(entry.Scene)
               && !string.Equals(entry.Scene, "shell", StringComparison.OrdinalIgnoreCase);

        private static void SetVisible(Control control, bool visible)
        {
            if (control == null || !GodotObject.IsInstanceValid(control))
                return;

            if (control is Widget widget)
                widget.Visible = visible;
            else
                control.Visible = visible;
        }
    }
}
