using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Mods.Rpg;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// Properties of the selected map event: id, cell, look (charset path, tileset index
    /// or item picture), facing, trigger, solidity, what it runs (scenario path,
    /// inline DSL lines, or a teleport) and the condition that shows it.
    /// <para/>
    /// A teleport is on when its map or cell is filled in; clearing the teleport
    /// map and both coordinates turns it off again. Solidity has a third state,
    /// "auto": solid when the event is drawn. Empty text fields are stored as
    /// null so the saved file stays short.
    /// </summary>
    public partial class RpgEventPropertiesPanel : VBoxContainer
    {
        private static readonly string[] TriggerKeys = { "tools/rpg_editor/trigger_action", "tools/rpg_editor/trigger_touch" };
        private static readonly string[] SolidKeys = { "tools/rpg_editor/solid_auto", "tools/rpg_editor/solid_yes", "tools/rpg_editor/solid_no" };

        private readonly Label emptyHint;
        private readonly VBoxContainer body;
        private readonly LineEdit idEdit;
        private readonly SpinBox xSpin;
        private readonly SpinBox ySpin;
        private readonly LineEdit spriteEdit;
        private readonly SpinBox tileSpin;
        private readonly LineEdit iconEdit;
        private readonly OptionButton dirPicker;
        private readonly OptionButton triggerPicker;
        private readonly OptionButton solidPicker;
        private readonly LineEdit scenarioEdit;
        private readonly TextEdit commandsEdit;
        private readonly LineEdit conditionEdit;
        private readonly LineEdit teleportMap;
        private readonly SpinBox teleportX;
        private readonly SpinBox teleportY;
        private readonly OptionButton teleportDir;
        private RpgMapEvent mapEvent;
        private bool suppress;
        private bool editing;

        /// <summary>Raised once before the first change after a bind.</summary>
        public event Action EditStarting;

        /// <summary>A property changed.</summary>
        public event Action Changed;

        /// <summary>The author pressed "delete".</summary>
        public event Action DeleteRequested;

        /// <summary>Builds the fields.</summary>
        public RpgEventPropertiesPanel()
        {
            AddChild(ToolUi.Section("tools/rpg_editor/section_event"));
            emptyHint = ToolUi.Hint(ToolUi.Tr("tools/rpg_editor/no_event"));
            AddChild(emptyHint);
            body = new VBoxContainer();
            AddChild(body);

            idEdit = ToolUi.Field(body, "tools/rpg_editor/event_id", ToolUi.Text("", OnTextChanged));
            xSpin = ToolUi.Field(body, "tools/rpg_editor/x", ToolUi.Number(0, 255, 1, OnNumberChanged));
            ySpin = ToolUi.Field(body, "tools/rpg_editor/y", ToolUi.Number(0, 255, 1, OnNumberChanged));
            spriteEdit = ToolUi.Field(body, "tools/rpg_editor/sprite", ToolUi.Text("village/elder", OnTextChanged), "tools/rpg_editor/sprite_tip");
            tileSpin = ToolUi.Field(body, "tools/rpg_editor/tile", ToolUi.Number(-1, 4095, 1, OnNumberChanged), "tools/rpg_editor/tile_tip");
            iconEdit = ToolUi.Field(body, "tools/rpg_editor/icon", ToolUi.Text("rpg_demo/branch_bundle", OnTextChanged), "tools/rpg_editor/icon_tip");
            dirPicker = ToolUi.Field(body, "tools/rpg_editor/dir", RpgEditorFields.Direction(OnDirectionPicked));
            triggerPicker = ToolUi.Field(body, "tools/rpg_editor/trigger", RpgEditorFields.Choice(TriggerKeys, OnChoicePicked));
            solidPicker = ToolUi.Field(body, "tools/rpg_editor/solid", RpgEditorFields.Choice(SolidKeys, OnChoicePicked));
            conditionEdit = ToolUi.Field(body, "tools/rpg_editor/condition", ToolUi.Text("flag_name != 1", OnTextChanged), "tools/rpg_editor/condition_tip");

            body.AddChild(ToolUi.Section("tools/rpg_editor/section_action"));
            scenarioEdit = ToolUi.Field(body, "tools/rpg_editor/scenario", ToolUi.Text("my_game/events/elder", OnTextChanged), "tools/rpg_editor/scenario_tip");
            body.AddChild(ToolUi.Hint(ToolUi.Tr("tools/rpg_editor/commands")));
            commandsEdit = ToolUi.Code();
            commandsEdit.CustomMinimumSize = new Vector2(0, 90);
            commandsEdit.SizeFlagsVertical = SizeFlags.Fill;
            commandsEdit.TextChanged += OnCommandsChanged;
            body.AddChild(commandsEdit);

            body.AddChild(ToolUi.Section("tools/rpg_editor/section_teleport"));
            teleportMap = ToolUi.Field(body, "tools/rpg_editor/teleport_map", ToolUi.Text("", OnTextChanged), "tools/rpg_editor/teleport_map_tip");
            teleportX = ToolUi.Field(body, "tools/rpg_editor/x", ToolUi.Number(-1, 255, 1, OnNumberChanged));
            teleportY = ToolUi.Field(body, "tools/rpg_editor/y", ToolUi.Number(-1, 255, 1, OnNumberChanged));
            teleportDir = ToolUi.Field(body, "tools/rpg_editor/dir", RpgEditorFields.Direction(OnDirectionPicked));

            body.AddChild(ToolUi.Button("tools/rpg_editor/delete_event", OnDeletePressed));
            Bind(null);
        }

        /// <summary>Shows an event (null: the hint instead of the fields).</summary>
        public void Bind(RpgMapEvent value)
        {
            mapEvent = value;
            editing = false;
            emptyHint.Visible = value == null;
            body.Visible = value != null;
            if (value == null)
                return;

            suppress = true;
            idEdit.Text = value.Id ?? string.Empty;
            xSpin.Value = value.X;
            ySpin.Value = value.Y;
            spriteEdit.Text = value.Sprite ?? string.Empty;
            tileSpin.Value = value.Tile;
            iconEdit.Text = value.Icon ?? string.Empty;
            dirPicker.Select((int)value.Dir);
            triggerPicker.Select((int)value.Trigger);
            solidPicker.Select(value.Solid == null ? 0 : value.Solid.Value ? 1 : 2);
            conditionEdit.Text = value.Condition ?? string.Empty;
            scenarioEdit.Text = value.Scenario ?? string.Empty;
            commandsEdit.Text = string.Join("\n", value.Commands ?? new List<string>());
            teleportMap.Text = value.Teleport?.Map ?? string.Empty;
            teleportX.Value = value.Teleport?.X ?? -1;
            teleportY.Value = value.Teleport?.Y ?? -1;
            teleportDir.Select((int)(value.Teleport?.Dir ?? RpgDirection.Down));
            suppress = false;
        }

        /// <summary>Re-reads the cell after the event was dragged on the canvas.</summary>
        public void RefreshCell()
        {
            if (mapEvent == null)
                return;
            suppress = true;
            xSpin.Value = mapEvent.X;
            ySpin.Value = mapEvent.Y;
            suppress = false;
        }

        private void OnTextChanged(string text) => Write();

        private void OnNumberChanged(double value) => Write();

        private void OnDirectionPicked(RpgDirection dir) => Write();

        private void OnChoicePicked(int index) => Write();

        private void OnCommandsChanged() => Write();

        private void OnDeletePressed()
        {
            if (mapEvent != null)
                DeleteRequested?.Invoke();
        }

        private void Write()
        {
            if (suppress || mapEvent == null)
                return;
            if (!editing)
            {
                EditStarting?.Invoke();
                editing = true;
            }

            mapEvent.Id = idEdit.Text.Trim();
            mapEvent.X = (int)xSpin.Value;
            mapEvent.Y = (int)ySpin.Value;
            mapEvent.Sprite = NullIfEmpty(spriteEdit.Text);
            mapEvent.Tile = (int)tileSpin.Value;
            mapEvent.Icon = NullIfEmpty(iconEdit.Text);
            mapEvent.Dir = (RpgDirection)dirPicker.Selected;
            mapEvent.Trigger = (RpgEventTrigger)triggerPicker.Selected;
            mapEvent.Solid = solidPicker.Selected switch { 1 => true, 2 => false, _ => null };
            mapEvent.Condition = NullIfEmpty(conditionEdit.Text);
            mapEvent.Scenario = NullIfEmpty(scenarioEdit.Text);
            var lines = commandsEdit.Text.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            mapEvent.Commands = lines.Count > 0 ? lines : null;

            var hasTeleport = teleportMap.Text.Trim().Length > 0 || teleportX.Value >= 0 || teleportY.Value >= 0;
            mapEvent.Teleport = hasTeleport
                ? new RpgTeleport
                {
                    Map = NullIfEmpty(teleportMap.Text),
                    X = Math.Max(0, (int)teleportX.Value),
                    Y = Math.Max(0, (int)teleportY.Value),
                    Dir = (RpgDirection)teleportDir.Selected,
                }
                : null;
            Changed?.Invoke();
        }

        private static string NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
