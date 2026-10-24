using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Mods.Rpg;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// The map's own properties in the right column: id (the scene name, read
    /// only — it is the file name), display name key, tileset, size, zoom, fixed
    /// ambient light (empty: the time of day lights the map), spawn
    /// and random encounters. The size is applied with a button, because a
    /// smaller size cuts cells off. Encounter groups are written as
    /// <c>slime; wolf,slime</c> — groups split by semicolons.
    /// <para/>
    /// The first edit after <see cref="Bind"/> raises <see cref="EditStarting"/>
    /// once, so typing into a field is one undo step, not one per letter.
    /// </summary>
    public partial class RpgMapPropertiesPanel : VBoxContainer
    {
        private readonly List<string> tilesets = new();
        private readonly Label idLabel;
        private readonly LineEdit nameEdit;
        private readonly OptionButton tilesetPicker;
        private readonly SpinBox widthSpin;
        private readonly SpinBox heightSpin;
        private readonly SpinBox zoomSpin;
        private readonly LineEdit ambientEdit;
        private readonly SpinBox spawnX;
        private readonly SpinBox spawnY;
        private readonly OptionButton spawnDir;
        private readonly SpinBox stepsSpin;
        private readonly LineEdit groupsEdit;
        private RpgMapDocument document;
        private bool suppress;
        private bool editing;

        /// <summary>Raised once before the first change after a bind.</summary>
        public event Action EditStarting;

        /// <summary>A property changed.</summary>
        public event Action Changed;

        /// <summary>The tileset changed: the window reloads the atlas.</summary>
        public event Action TilesetChanged;

        /// <summary>The author asked for a new size.</summary>
        public event Action<int, int> ResizeRequested;

        /// <summary>Builds the fields.</summary>
        public RpgMapPropertiesPanel()
        {
            AddChild(ToolUi.Section("tools/rpg_editor/section_map"));
            idLabel = ToolUi.Field(this, "tools/rpg_editor/map_id", new Label(), "tools/rpg_editor/map_id_tip");
            nameEdit = ToolUi.Field(this, "tools/rpg_editor/map_name", ToolUi.Text("rpg/map/name", OnNameChanged), "tools/rpg_editor/map_name_tip");
            tilesetPicker = ToolUi.Field(this, "tools/rpg_editor/tileset", new OptionButton());
            tilesetPicker.ItemSelected += OnTilesetPicked;

            widthSpin = ToolUi.Field(this, "tools/rpg_editor/width", ToolUi.Number(1, 256, 1, null));
            heightSpin = ToolUi.Field(this, "tools/rpg_editor/height", ToolUi.Number(1, 256, 1, null));
            AddChild(ToolUi.Button("tools/rpg_editor/resize", OnResizePressed));
            zoomSpin = ToolUi.Field(this, "tools/rpg_editor/zoom", ToolUi.Number(1, 4, 1, OnZoomChanged), "tools/rpg_editor/zoom_tip");
            ambientEdit = ToolUi.Field(this, "tools/rpg_editor/ambient", ToolUi.Text("#e0d2b8", OnAmbientChanged), "tools/rpg_editor/ambient_tip");

            AddChild(ToolUi.Section("tools/rpg_editor/section_spawn"));
            spawnX = ToolUi.Field(this, "tools/rpg_editor/x", ToolUi.Number(0, 255, 1, OnSpawnChanged));
            spawnY = ToolUi.Field(this, "tools/rpg_editor/y", ToolUi.Number(0, 255, 1, OnSpawnChanged));
            spawnDir = ToolUi.Field(this, "tools/rpg_editor/dir", RpgEditorFields.Direction(OnSpawnDirPicked));

            AddChild(ToolUi.Section("tools/rpg_editor/section_encounters"));
            stepsSpin = ToolUi.Field(this, "tools/rpg_editor/encounter_steps", ToolUi.Number(0, 999, 1, OnEncountersChanged), "tools/rpg_editor/encounter_steps_tip");
            groupsEdit = ToolUi.Field(this, "tools/rpg_editor/encounter_groups", ToolUi.Text("slime; wolf,slime", OnGroupsChanged), "tools/rpg_editor/encounter_groups_tip");
        }

        /// <summary>Shows a document's map (null clears).</summary>
        public void Bind(RpgMapDocument value)
        {
            document = value;
            editing = false;
            suppress = true;
            tilesets.Clear();
            tilesets.AddRange(Entities.Collect<RpgTileset>().Keys.OrderBy(k => k));
            tilesetPicker.Clear();
            foreach (var id in tilesets)
                tilesetPicker.AddItem(id);

            var map = value?.Map;
            Visible = map != null;
            if (map != null)
            {
                idLabel.Text = map.ID;
                nameEdit.Text = map.Name ?? string.Empty;
                tilesetPicker.Select(tilesets.IndexOf(map.Tileset ?? string.Empty));
                widthSpin.Value = value.Grid.Width;
                heightSpin.Value = value.Grid.Height;
                zoomSpin.Value = map.Zoom;
                ambientEdit.Text = map.Ambient ?? string.Empty;
                spawnX.Value = map.Spawn.X;
                spawnY.Value = map.Spawn.Y;
                spawnDir.Select((int)map.Spawn.Dir);
                stepsSpin.Value = map.Encounters?.Steps ?? 0;
                groupsEdit.Text = string.Join("; ", map.Encounters?.Groups ?? new List<string>());
            }
            suppress = false;
        }

        private bool Begin()
        {
            if (suppress || document == null)
                return false;
            if (!editing)
            {
                EditStarting?.Invoke();
                editing = true;
            }
            return true;
        }

        private void OnNameChanged(string text)
        {
            if (!Begin())
                return;
            document.Map.Name = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            Changed?.Invoke();
        }

        private void OnAmbientChanged(string text)
        {
            if (!Begin())
                return;
            document.Map.Ambient = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            Changed?.Invoke();
        }

        private void OnTilesetPicked(long index)
        {
            if (!Begin() || index < 0 || index >= tilesets.Count)
                return;
            document.Map.Tileset = tilesets[(int)index];
            Changed?.Invoke();
            TilesetChanged?.Invoke();
        }

        private void OnResizePressed()
        {
            if (document != null)
                ResizeRequested?.Invoke((int)widthSpin.Value, (int)heightSpin.Value);
        }

        private void OnZoomChanged(double value)
        {
            if (!Begin())
                return;
            document.Map.Zoom = (int)value;
            Changed?.Invoke();
        }

        private void OnSpawnChanged(double value)
        {
            if (!Begin())
                return;
            document.Map.Spawn.X = Math.Clamp((int)spawnX.Value, 0, document.Grid.Width - 1);
            document.Map.Spawn.Y = Math.Clamp((int)spawnY.Value, 0, document.Grid.Height - 1);
            Changed?.Invoke();
        }

        private void OnSpawnDirPicked(RpgDirection dir)
        {
            if (!Begin())
                return;
            document.Map.Spawn.Dir = dir;
            Changed?.Invoke();
        }

        private void OnEncountersChanged(double value) => WriteEncounters();

        private void OnGroupsChanged(string text) => WriteEncounters();

        private void WriteEncounters()
        {
            if (!Begin())
                return;
            var groups = groupsEdit.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(g => g.Replace(" ", string.Empty))
                .Where(g => g.Length > 0)
                .ToList();
            var steps = (int)stepsSpin.Value;
            document.Map.Encounters = steps > 0 || groups.Count > 0 ? new RpgEncounterTable { Steps = steps, Groups = groups } : null;
            Changed?.Invoke();
        }
    }
}
