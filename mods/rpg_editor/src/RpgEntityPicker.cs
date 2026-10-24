using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Mods.Interface.Theming;
using Cthangover.Tools.Common;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// A tab of the editor's left column listing entities of the game (the
    /// characters, the items) with a picture, a search over name and id and
    /// buttons that act on the selected row. The window decides what the
    /// entries are (<see cref="Source"/>) and what the buttons do
    /// (<see cref="AddAction"/>); placing on the map is then a click on a cell.
    /// <para/>
    /// The list is read again on every <see cref="Reload"/> (the window calls it
    /// whenever the tab is opened), so entities added to the game's data while
    /// the editor is open show up.
    /// </summary>
    public partial class RpgEntityPicker : VBoxContainer
    {
        private readonly List<RpgPickerEntry> shown = new();
        private readonly List<Button> actions = new();
        private readonly LineEdit search;
        private readonly ItemList list;
        private readonly HBoxContainer options;
        private readonly HBoxContainer buttons;
        private List<RpgPickerEntry> all = new();

        /// <summary>
        /// Builds the option and button rows with their hint on top — the list grows with the
        /// entries (<c>AutoHeight</c>), so buttons below it would scroll out of sight — then the
        /// search field and the list.
        /// </summary>
        public RpgEntityPicker(string searchKey, string hintKey)
        {
            options = new HBoxContainer();
            AddChild(options);
            buttons = new HBoxContainer();
            AddChild(buttons);
            AddChild(ToolUi.Hint(ToolUi.Tr(hintKey)));

            search = ToolUi.Text(ToolUi.Tr(searchKey), OnSearchChanged);
            AddChild(search);

            list = new ItemList
            {
                AutoHeight = true,
                FixedIconSize = new Vector2I(32, 48),
                TextureFilter = TextureFilterEnum.Nearest,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            list.ItemSelected += OnItemSelected;
            AddChild(list);
        }

        /// <summary>Reads the entries; called by <see cref="Reload"/>.</summary>
        public Func<List<RpgPickerEntry>> Source { get; set; }

        /// <summary>The selected entry, or null.</summary>
        public RpgPickerEntry Selected
        {
            get
            {
                var picked = list.GetSelectedItems();
                return picked.Length == 0 ? null : shown[picked[0]];
            }
        }

        /// <summary>Adds a button acting on the selected entry; it is disabled while nothing is selected.</summary>
        public void AddAction(string textKey, string tooltipKey, Action<RpgPickerEntry> action)
        {
            var button = ToolUi.Button(textKey, null, ButtonKind.Secondary, tooltipKey);
            button.Pressed += () => Run(action);
            button.Disabled = true;
            actions.Add(button);
            buttons.AddChild(button);
        }

        /// <summary>Adds a control (a count, a mode) to the row above the buttons.</summary>
        public void AddOption(Control control) => options.AddChild(control);

        /// <summary>Reads the entries again and reapplies the search.</summary>
        public void Reload()
        {
            all = Source?.Invoke() ?? new List<RpgPickerEntry>();
            Filter();
        }

        private void Filter()
        {
            var selectedId = Selected?.Id;
            var query = search.Text.Trim();
            shown.Clear();
            shown.AddRange(all.Where(entry => query.Length == 0
                || entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase)));

            list.Clear();
            foreach (var entry in shown)
            {
                var index = list.AddItem($"{entry.Name}  ·  {entry.Id}", entry.Icon);
                list.SetItemTooltip(index, entry.Look ?? ToolUi.Tr("tools/rpg_editor/picker_no_look"));
                if (entry.Id == selectedId)
                    list.Select(index);
            }
            UpdateButtons();
        }

        private void Run(Action<RpgPickerEntry> action)
        {
            if (Selected != null)
                action(Selected);
        }

        private void UpdateButtons()
        {
            foreach (var button in actions)
                button.Disabled = Selected == null;
        }

        private void OnSearchChanged(string text) => Filter();

        private void OnItemSelected(long index) => UpdateButtons();
    }
}
