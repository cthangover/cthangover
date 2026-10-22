using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Theming;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Rpg;
using Cthangover.Tools.Common;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// Editor of RPG tile maps (<c>maps/*.json</c>, kind <c>rpg_map</c>).
    /// Layout: toolbar (file, save, layer, brush, grid, zoom, undo/redo, play),
    /// the tile palette on the left, the map in the middle, the map's and the
    /// selected event's properties on the right.
    /// <para/>
    /// Layers are the game's: back, main, front, the collision layer (codes
    /// over the tiles' own solidity) and events. Every edit is undoable
    /// (Ctrl+Z / Ctrl+Y); Ctrl+S saves. A new file becomes a map filled with the
    /// first tile of the first tileset, and the first save also writes
    /// <c>scenes/&lt;id&gt;.json</c> when the mod has no such scene, because the
    /// map id is the scene name; the game re-reads its scenes right away
    /// (<see cref="SceneFileService.RefreshGame"/>), so "Play" works on a new map
    /// without a mod reload.
    /// <para/>
    /// After a save the map is put into the running game's map cache, and a
    /// world showing it is remounted, so the edit is visible at once; "Play"
    /// saves and moves the player onto the map at its spawn.
    /// </summary>
    public partial class RpgMapEditorWindow : ToolWindow
    {
        private const string MapDir = "maps";
        private const string SceneDir = "scenes";

        private readonly RpgTextures textures = new();
        private readonly Dictionary<RpgEditLayer, Button> layerButtons = new();
        private readonly Dictionary<RpgEditTool, Button> toolButtons = new();

        private ModFileSelector fileSelector;
        private RpgTilePalette palette;
        private RpgMapEditorCanvas canvas;
        private RpgMapPropertiesPanel mapPanel;
        private RpgEventPropertiesPanel eventPanel;
        private CheckBox gridCheck;
        private Label zoomLabel;

        private RpgMapDocument document;
        private string modId;
        private string path;
        private bool isNew;

        /// <summary>Builds the window and opens the first map found.</summary>
        public RpgMapEditorWindow() : base("tools/rpg_editor/title")
        {
            CthangoverTheme.Apply(this);
            BuildUi();
            SetHint(ToolUi.Tr("tools/rpg_editor/hint"));
            SelectLayer(RpgEditLayer.Back);
            SelectTool(RpgEditTool.Pencil);
            fileSelector.Refresh();
        }

        // ── layout ─────────────────────────────────────────────────

        private void BuildUi()
        {
            var outer = CreateFillContainer();
            AddChild(outer);
            outer.AddChild(BuildToolbar());

            var main = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            outer.AddChild(main);

            var paletteColumn = new VBoxContainer();
            paletteColumn.AddChild(ToolUi.Section("tools/rpg_editor/section_palette"));
            palette = new RpgTilePalette();
            palette.TileSelected += OnPaletteTile;
            palette.CollisionSelected += OnPaletteCollision;
            paletteColumn.AddChild(palette);
            main.AddChild(CreateSidebar(paletteColumn, 400));

            var center = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            main.AddChild(center);

            canvas = new RpgMapEditorCanvas();
            canvas.EditStarting += RecordUndo;
            canvas.Changed += OnDocumentChanged;
            canvas.EventSelected += OnEventSelected;
            canvas.TilePicked += OnTilePicked;
            center.AddChild(Scroll(canvas));

            var properties = new VBoxContainer();
            mapPanel = new RpgMapPropertiesPanel();
            mapPanel.EditStarting += RecordUndo;
            mapPanel.Changed += OnDocumentChanged;
            mapPanel.TilesetChanged += LoadTileset;
            mapPanel.ResizeRequested += OnResizeRequested;
            properties.AddChild(mapPanel);
            eventPanel = new RpgEventPropertiesPanel();
            eventPanel.EditStarting += RecordUndo;
            eventPanel.Changed += OnDocumentChanged;
            eventPanel.DeleteRequested += DeleteSelectedEvent;
            properties.AddChild(eventPanel);
            center.AddChild(CreateSidebar(properties, 340));
        }

        private HBoxContainer BuildToolbar()
        {
            var toolbar = CreateToolbar();
            fileSelector = new ModFileSelector(MapDir, ".json");
            fileSelector.FileRequested += OnFileRequested;
            toolbar.AddChild(fileSelector);
            toolbar.AddChild(ToolUi.Button("tools/common/save", OnSavePressed, ButtonKind.Primary, "tools/common/save_tip"));
            toolbar.AddChild(ToolUi.Divider());

            AddLayerButton(toolbar, RpgEditLayer.Back, "tools/rpg_editor/layer_back");
            AddLayerButton(toolbar, RpgEditLayer.Main, "tools/rpg_editor/layer_main");
            AddLayerButton(toolbar, RpgEditLayer.Front, "tools/rpg_editor/layer_front");
            AddLayerButton(toolbar, RpgEditLayer.Collision, "tools/rpg_editor/layer_collision");
            AddLayerButton(toolbar, RpgEditLayer.Events, "tools/rpg_editor/layer_events");
            toolbar.AddChild(ToolUi.Divider());

            AddToolButton(toolbar, RpgEditTool.Pencil, "tools/rpg_editor/tool_pencil");
            AddToolButton(toolbar, RpgEditTool.Eraser, "tools/rpg_editor/tool_eraser");
            AddToolButton(toolbar, RpgEditTool.Fill, "tools/rpg_editor/tool_fill");
            toolbar.AddChild(ToolUi.Divider());

            gridCheck = new CheckBox { Text = ToolUi.Tr("tools/rpg_editor/grid"), ButtonPressed = true };
            gridCheck.Toggled += OnGridToggled;
            toolbar.AddChild(gridCheck);
            var zoomOut = UiKit.IconButton("−", ToolUi.Tr("tools/rpg_editor/zoom_out"));
            zoomOut.Pressed += OnZoomOut;
            toolbar.AddChild(zoomOut);
            zoomLabel = new Label { CustomMinimumSize = new Vector2(48, 0), HorizontalAlignment = HorizontalAlignment.Center };
            toolbar.AddChild(zoomLabel);
            var zoomIn = UiKit.IconButton("+", ToolUi.Tr("tools/rpg_editor/zoom_in"));
            zoomIn.Pressed += OnZoomIn;
            toolbar.AddChild(zoomIn);
            toolbar.AddChild(ToolUi.Divider());

            toolbar.AddChild(ToolUi.Button("tools/rpg_editor/undo", Undo, ButtonKind.Secondary, "tools/rpg_editor/undo_tip"));
            toolbar.AddChild(ToolUi.Button("tools/rpg_editor/redo", Redo, ButtonKind.Secondary, "tools/rpg_editor/redo_tip"));
            toolbar.AddChild(ToolUi.Button("tools/rpg_editor/play", Play, ButtonKind.Secondary, "tools/rpg_editor/play_tip"));
            return toolbar;
        }

        private void AddLayerButton(HBoxContainer toolbar, RpgEditLayer layer, string key)
        {
            var button = ToolUi.Button(key, null);
            button.ToggleMode = true;
            button.Pressed += () => SelectLayer(layer);
            layerButtons[layer] = button;
            toolbar.AddChild(button);
        }

        private void AddToolButton(HBoxContainer toolbar, RpgEditTool tool, string key)
        {
            var button = ToolUi.Button(key, null);
            button.ToggleMode = true;
            button.Pressed += () => SelectTool(tool);
            toolButtons[tool] = button;
            toolbar.AddChild(button);
        }

        private static ScrollContainer Scroll(Control content)
        {
            var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(content);
            return scroll;
        }

        // ── file ───────────────────────────────────────────────────

        private void OnFileRequested(string mod, string file)
        {
            RunDiscardingChanges(() => Open(mod, file), () => fileSelector.Show(modId, path));
        }

        private void Open(string mod, string file)
        {
            modId = mod;
            path = file;
            document = null;
            isNew = false;
            MarkClean();

            if (mod == null)
            {
                SetStatus(ToolUi.Tr("tools/common/no_writable_mods"), ToolStatusKind.Error);
            }
            else if (file == null)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/common/no_files"), mod));
            }
            else
            {
                try
                {
                    var text = ModFileService.ReadText(mod, file);
                    isNew = string.IsNullOrEmpty(text);
                    document = isNew ? CreateDocument(file) : RpgMapDocument.Load(text);
                    SetStatus(string.Format(ToolUi.Tr(isNew ? "tools/common/new_file_status" : "tools/common/loaded"), mod + "/" + file));
                }
                catch (Exception ex)
                {
                    SetStatus($"{ToolUi.Tr("tools/common/load_error")}: {ex.Message}", ToolStatusKind.Error);
                    GameLogger.Log("RPG_EDITOR", $"load failed for '{mod}/{file}': {ex.Message}", LogLevel.Error);
                }
            }

            fileSelector.Show(modId, path);
            canvas.SelectedEvent = null;
            BindDocument();
        }

        private static RpgMapDocument CreateDocument(string file)
        {
            var tileset = Entities.Collect<RpgTileset>().Keys.OrderBy(k => k).FirstOrDefault();
            return RpgMapDocument.Create(Path.GetFileNameWithoutExtension(file), tileset, 20, 15, 0);
        }

        /// <summary>Ctrl+S.</summary>
        protected override void OnSaveShortcut() => Save();

        private void OnSavePressed() => Save();

        private bool Save()
        {
            if (document == null || path == null)
            {
                SetStatus(ToolUi.Tr("tools/common/no_file_to_save"), ToolStatusKind.Error);
                return false;
            }

            var problems = document.Validate();
            if (problems.Count > 0)
            {
                SetStatus(string.Format(ToolUi.Tr(problems[0].Key), problems[0].Argument), ToolStatusKind.Error);
                return false;
            }

            var json = document.ToJson();
            if (!ModFileService.WriteText(modId, path, json))
            {
                SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                return false;
            }

            var sceneNote = isNew ? EnsureScene() : null;
            if (sceneNote != null)
                SceneFileService.RefreshGame();
            isNew = false;
            MarkClean();
            PublishToGame(json);
            SetStatus(string.Format(ToolUi.Tr("tools/common/saved"), modId + "/" + path) + (sceneNote ?? string.Empty), ToolStatusKind.Ok);
            return true;
        }

        /// <summary>Writes <c>scenes/&lt;id&gt;.json</c> for a new map when the mod has none; returns a status suffix.</summary>
        private string EnsureScene()
        {
            var scenePath = SceneDir + "/" + document.Map.ID + ".json";
            if (!string.IsNullOrEmpty(ModFileService.ReadText(modId, scenePath)))
                return null;
            var json = "{\n  \"name\": \"" + document.Map.ID + "\",\n  \"defaultAmbient\": \"\"\n}\n";
            return ModFileService.WriteText(modId, scenePath, json)
                ? " · " + string.Format(ToolUi.Tr("tools/rpg_editor/scene_created"), scenePath)
                : null;
        }

        private static void PublishToGame(string json)
        {
            var copy = RpgMapDocument.Load(json).Map;
            RpgMapFactory.Instance.Publish(copy);
            var host = RpgWorldHost.Current;
            if (host?.View?.Map?.ID == copy.ID)
                host.Mount(copy.ID);
        }

        private void Play()
        {
            if (!Save())
                return;
            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
            if (sceneManager == null || !sceneManager.HasScene(document.Map.ID))
            {
                SetStatus(string.Format(ToolUi.Tr("tools/rpg_editor/play_no_scene"), document.Map.ID), ToolStatusKind.Error);
                return;
            }
            var spawn = document.Map.Spawn;
            RpgWorldHost.Teleport(new RpgTeleport { Map = document.Map.ID, X = spawn.X, Y = spawn.Y, Dir = spawn.Dir });
        }

        // ── editing ────────────────────────────────────────────────

        private void BindDocument()
        {
            canvas.Document = document;
            mapPanel.Bind(document);
            eventPanel.Bind(canvas.SelectedEvent);
            LoadTileset();
        }

        private void LoadTileset()
        {
            var tileset = document == null ? null : RpgTilesetFactory.Instance.Get(document.Map.Tileset);
            var atlas = tileset == null ? null : textures.Tileset(tileset.Image);
            canvas.Tileset = tileset;
            canvas.Atlas = atlas;
            palette.SetTileset(tileset, atlas);
            canvas.Refresh();
            UpdateZoomLabel();
        }

        private void RecordUndo()
        {
            document?.BeginChange();
            SetDirty();
        }

        private void OnDocumentChanged()
        {
            SetDirty();
            canvas.Refresh();
            eventPanel.RefreshCell();
        }

        private void Undo() => StepHistory(document != null && document.Undo());

        private void Redo() => StepHistory(document != null && document.Redo());

        private void StepHistory(bool stepped)
        {
            if (!stepped)
                return;
            // The document holds a new map object now: find the selection again by id.
            var selectedId = canvas.SelectedEvent?.Id;
            canvas.SelectedEvent = document.Map.Events.FirstOrDefault(e => e.Id == selectedId);
            SetDirty();
            BindDocument();
        }

        private void OnResizeRequested(int width, int height)
        {
            if (document == null || (width == document.Grid.Width && height == document.Grid.Height))
                return;
            RecordUndo();
            document.Resize(width, height);
            mapPanel.Bind(document);
            canvas.Refresh();
        }

        private void DeleteSelectedEvent()
        {
            if (document == null || canvas.SelectedEvent == null)
                return;
            RecordUndo();
            document.RemoveEvent(canvas.SelectedEvent);
            canvas.SelectedEvent = null;
            eventPanel.Bind(null);
            canvas.Refresh();
        }

        private void OnEventSelected(RpgMapEvent mapEvent) => eventPanel.Bind(mapEvent);

        private void OnPaletteTile(int index)
        {
            canvas.SelectedTile = index;
            if (canvas.ActiveLayer > RpgEditLayer.Front)
                SelectLayer(RpgEditLayer.Main);
        }

        private void OnPaletteCollision(char code) => canvas.SelectedCollision = code;

        private void OnTilePicked(int index)
        {
            canvas.SelectedTile = index;
            palette.Select(index);
        }

        private void SelectLayer(RpgEditLayer layer)
        {
            canvas.ActiveLayer = layer;
            foreach (var pair in layerButtons)
                pair.Value.SetPressedNoSignal(pair.Key == layer);
            palette.SetCollisionMode(layer == RpgEditLayer.Collision);
            foreach (var button in toolButtons.Values)
                button.Disabled = layer == RpgEditLayer.Events;
            canvas.QueueRedraw();
        }

        private void SelectTool(RpgEditTool tool)
        {
            canvas.Tool = tool;
            foreach (var pair in toolButtons)
                pair.Value.SetPressedNoSignal(pair.Key == tool);
        }

        private void OnGridToggled(bool on)
        {
            canvas.ShowGrid = on;
            canvas.QueueRedraw();
        }

        private void OnZoomIn()
        {
            canvas.SetZoom(canvas.Zoom * 1.25f);
            UpdateZoomLabel();
        }

        private void OnZoomOut()
        {
            canvas.SetZoom(canvas.Zoom * 0.8f);
            UpdateZoomLabel();
        }

        private void UpdateZoomLabel() => zoomLabel.Text = $"{canvas.Zoom * 100f:0}%";

        /// <summary>Shortcuts: Ctrl+Z / Ctrl+Y undo and redo, 1–5 pick the layer, B/E/G the brush, Del deletes the selected event.</summary>
        protected override bool HandleKey(InputEventKey key)
        {
            if (key.CtrlPressed && key.Keycode == Key.Z)
            {
                if (key.ShiftPressed)
                    Redo();
                else
                    Undo();
                return true;
            }
            if (key.CtrlPressed && key.Keycode == Key.Y)
            {
                Redo();
                return true;
            }
            if (key.CtrlPressed || key.AltPressed)
                return false;

            switch (key.Keycode)
            {
                case Key.Key1: SelectLayer(RpgEditLayer.Back); return true;
                case Key.Key2: SelectLayer(RpgEditLayer.Main); return true;
                case Key.Key3: SelectLayer(RpgEditLayer.Front); return true;
                case Key.Key4: SelectLayer(RpgEditLayer.Collision); return true;
                case Key.Key5: SelectLayer(RpgEditLayer.Events); return true;
                case Key.B: SelectTool(RpgEditTool.Pencil); return true;
                case Key.E: SelectTool(RpgEditTool.Eraser); return true;
                case Key.G: SelectTool(RpgEditTool.Fill); return true;
                case Key.Delete: DeleteSelectedEvent(); return true;
            }
            return false;
        }
    }
}
