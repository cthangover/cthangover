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
    /// the tile palette, the game's characters and items as tabs on the left, the
    /// map in the middle, the map's and the selected event's properties on the right.
    /// A character is put on the map as an NPC or an enemy, an item as a pickup:
    /// pick it, press the button, click a cell (<see cref="RpgMapDocument.AddCharacter"/>,
    /// <see cref="RpgMapDocument.AddItem"/>).
    /// <para/>
    /// Layers are the game's: back, main, front, the collision layer (codes
    /// over the tiles' own solidity) and events. Every edit is undoable
    /// (Ctrl+Z / Ctrl+Y); Ctrl+S saves. A new file becomes a map filled with the
    /// first tile of the first tileset, and the first save also writes
    /// <c>scenes/&lt;id&gt;.json</c> when the mod has no such scene, because the
    /// map id is the scene name; the game re-reads its scenes right away
    /// (<see cref="SceneManager.ReloadContent"/>), so "Play" works on a new map
    /// without a mod reload.
    /// <para/>
    /// Saving writes the map file and the mod's events file of the map,
    /// <c>map_events/&lt;id&gt;/events.json</c> (<see cref="RpgMapDocument"/> explains
    /// own and locked events).
    /// <para/>
    /// After a save the map is put into the running game's map cache, and a
    /// world showing it is remounted, so the edit is visible at once; "Play"
    /// saves and moves the player onto the map at its spawn.
    /// </summary>
    public partial class RpgMapEditorWindow : ToolWindow
    {
        private const string MapDir = "maps";

        private readonly RpgTextures textures = new();
        private readonly Dictionary<RpgEditLayer, Button> layerButtons = new();
        private readonly Dictionary<RpgEditTool, Button> toolButtons = new();

        private ModFileSelector fileSelector;
        private TabContainer sideTabs;
        private RpgTilePalette palette;
        private SpinBox itemCount;
        private Func<Vector2I, RpgMapEvent> placeAt;
        private string placedKey;
        private string placedName;
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

            var leftColumn = new VBoxContainer();
            sideTabs = new TabContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            leftColumn.AddChild(sideTabs);

            var paletteColumn = new VBoxContainer();
            paletteColumn.AddChild(ToolUi.Section("tools/rpg_editor/section_palette"));
            palette = new RpgTilePalette();
            palette.TileSelected += OnPaletteTile;
            palette.CollisionSelected += OnPaletteCollision;
            paletteColumn.AddChild(palette);
            sideTabs.AddChild(paletteColumn);
            sideTabs.SetTabTitle(0, ToolUi.Tr("tools/rpg_editor/tab_tiles"));

            var characters = new RpgEntityPicker("tools/rpg_editor/character_search", "tools/rpg_editor/characters_hint") { Name = "Characters" };
            characters.Source = LoadCharacters;
            characters.AddAction("tools/rpg_editor/place_npc", "tools/rpg_editor/place_npc_tip", PlaceNpc);
            characters.AddAction("tools/rpg_editor/place_enemy", "tools/rpg_editor/place_enemy_tip", PlaceEnemy);
            sideTabs.AddChild(characters);
            sideTabs.SetTabTitle(1, ToolUi.Tr("tools/rpg_editor/tab_characters"));

            var items = new RpgEntityPicker("tools/rpg_editor/item_search", "tools/rpg_editor/items_hint") { Name = "Items" };
            items.Source = LoadItems;
            itemCount = ToolUi.Number(1, 999, 1, null);
            itemCount.Value = 1;
            itemCount.TooltipText = ToolUi.Tr("tools/rpg_editor/item_count_tip");
            items.AddOption(new Label { Text = ToolUi.Tr("tools/rpg_editor/item_count") });
            items.AddOption(itemCount);
            items.AddAction("tools/rpg_editor/place_item", "tools/rpg_editor/place_item_tip", PlaceItem);
            sideTabs.AddChild(items);
            sideTabs.SetTabTitle(2, ToolUi.Tr("tools/rpg_editor/tab_items"));
            sideTabs.TabChanged += OnSideTabChanged;
            main.AddChild(CreateSidebar(leftColumn, 400));

            var center = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            main.AddChild(center);

            canvas = new RpgMapEditorCanvas();
            canvas.EditStarting += RecordUndo;
            canvas.Changed += OnDocumentChanged;
            canvas.EventSelected += OnEventSelected;
            canvas.TilePicked += OnTilePicked;
            canvas.CellChosen += OnCellChosen;
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
                    document = isNew ? CreateDocument(file) : LoadDocument(mod, text);
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

        /// <summary>
        /// The map with the editor's events file of the same mod; every other event
        /// the game shows on the map is locked. An own event hides a locked one of the
        /// same id (another mod replacing it), so the author sees and edits their own.
        /// </summary>
        private static RpgMapDocument LoadDocument(string mod, string text)
        {
            var id = RpgMapDocument.Load(text).Map.ID;
            var eventsPath = RpgMapDocument.EventsPath(id);
            var document = RpgMapDocument.Load(text, ModFileService.ReadText(mod, eventsPath));
            var own = new HashSet<string>(document.Events.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
            document.SetLockedEvents(RpgMapEvents.Of(id)
                .Where(e => !(e.ModId == mod && e.File == eventsPath) && !own.Contains(e.Id))
                .ToList());
            return document;
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
            var eventsPath = RpgMapDocument.EventsPath(document.Map.ID);
            var writeEvents = document.Events.Count > 0 || !string.IsNullOrEmpty(ModFileService.ReadText(modId, eventsPath));
            if (!ModFileService.WriteText(modId, path, json)
                || (writeEvents && !ModFileService.WriteText(modId, eventsPath, document.EventsToJson())))
            {
                SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                return false;
            }
            RpgMapEvents.Invalidate();

            var sceneNote = isNew ? EnsureScene() : null;
            if (sceneNote != null)
                SceneManager.ReloadContent();
            isNew = false;
            MarkClean();
            PublishToGame(json);
            SetStatus(string.Format(ToolUi.Tr("tools/common/saved"), modId + "/" + path) + (sceneNote ?? string.Empty), ToolStatusKind.Ok);
            return true;
        }

        /// <summary>Writes <c>scenes/&lt;id&gt;.json</c> for a new map when the mod has none; returns a status suffix.</summary>
        private string EnsureScene()
        {
            var scene = new SceneFileEntry { ModId = modId, Name = document.Map.ID, RelativePath = SceneFileEntry.PathFor(document.Map.ID) };
            if (!string.IsNullOrEmpty(ModFileService.ReadText(modId, scene.RelativePath)))
                return null;
            return SceneFileService.Write(scene)
                ? " · " + string.Format(ToolUi.Tr("tools/rpg_editor/scene_created"), scene.RelativePath)
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
            canvas.SelectedEvent = document.Events.FirstOrDefault(e => e.Id == selectedId);
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

        private void OnSideTabChanged(long tab)
        {
            if (sideTabs.GetCurrentTabControl() is RpgEntityPicker picker)
                picker.Reload();
        }

        private List<RpgPickerEntry> LoadCharacters() => RpgCharacterCatalog.Load(textures);

        private List<RpgPickerEntry> LoadItems() => RpgItemCatalog.Load(textures);

        private void PlaceNpc(RpgPickerEntry entry)
            => BeginPlacing(entry, "tools/rpg_editor/placed_npc", cell => document.AddCharacter(cell.X, cell.Y, entry.Id, entry.Look, false));

        private void PlaceEnemy(RpgPickerEntry entry)
            => BeginPlacing(entry, "tools/rpg_editor/placed_enemy", cell => document.AddCharacter(cell.X, cell.Y, entry.Id, entry.Look, true));

        private void PlaceItem(RpgPickerEntry entry)
        {
            var count = (int)itemCount.Value;
            var line = string.Format(ToolUi.Tr("tools/rpg_editor/item_found_line"), entry.Name, count);
            BeginPlacing(entry, "tools/rpg_editor/placed_item", cell => document.AddItem(cell.X, cell.Y, entry.Id, entry.Look, count, line));
        }

        /// <summary>Something is to be placed: switch to the events layer and wait for a cell; <paramref name="place"/> makes the event.</summary>
        private void BeginPlacing(RpgPickerEntry entry, string doneKey, Func<Vector2I, RpgMapEvent> place)
        {
            if (document == null)
                return;
            placeAt = place;
            placedKey = doneKey;
            placedName = entry.Name;
            SelectLayer(RpgEditLayer.Events);
            canvas.Placing = true;
            SetStatus(string.Format(ToolUi.Tr("tools/rpg_editor/place_status"), entry.Name));
        }

        private void OnCellChosen(Vector2I? cell)
        {
            if (cell == null || placeAt == null || document == null)
            {
                SetStatus(ToolUi.Tr("tools/rpg_editor/place_cancelled"));
                return;
            }
            RecordUndo();
            var mapEvent = placeAt(cell.Value);
            canvas.SelectedEvent = mapEvent;
            eventPanel.Bind(mapEvent);
            OnDocumentChanged();
            SetStatus(string.Format(ToolUi.Tr(placedKey), placedName, mapEvent.Id), ToolStatusKind.Ok);
        }

        private void OnTilePicked(int index)
        {
            canvas.SelectedTile = index;
            palette.Select(index);
        }

        private void SelectLayer(RpgEditLayer layer)
        {
            canvas.ActiveLayer = layer;
            if (layer != RpgEditLayer.Events)
                canvas.Placing = false;
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
