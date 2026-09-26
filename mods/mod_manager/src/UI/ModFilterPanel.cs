using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Left filter sidebar. The frame (title, reset button, section captions and
    /// the grouping toggle) lives in
    /// <c>scenes/components/mod_filter_panel.tscn</c> for visual editing; this
    /// script fills the two list containers with per-status and per-category
    /// checkboxes and keeps the shared <see cref="ModFilterState"/> in sync.
    /// The panel does not own the state and never rebuilds the grid itself — it
    /// announces every change through <see cref="Changed"/>.
    /// </summary>
    public partial class ModFilterPanel : VBoxContainer
    {
        private ModFilterState _state;
        private Label _title;
        private Button _reset;
        private Label _statusCaption;
        private Label _categoryCaption;
        private CheckButton _groupToggle;
        private VBoxContainer _statusList;
        private VBoxContainer _categoryList;
        private readonly Dictionary<ModItemStatus, CheckBox> _statusBoxes = new();
        private readonly Dictionary<CategoryType, CheckBox> _categoryBoxes = new();

        /// <summary>Raised after any filter or grouping change.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised after the reset button cleared the state. The panel cannot
        /// touch the header's search box, so the screen clears it in response.
        /// </summary>
        public event Action ResetRequested;

        /// <summary>Binds the sidebar to the shared filter state; call once, after the scene is built.</summary>
        public void Bind(ModFilterState state)
        {
            _state = state;

            _title = GetNode<Label>("Title");
            _reset = GetNode<Button>("Reset");
            _statusCaption = GetNode<Label>("StatusCaption");
            _categoryCaption = GetNode<Label>("CategoryCaption");
            _groupToggle = GetNode<CheckButton>("GroupToggle");
            _statusList = GetNode<VBoxContainer>("StatusList");
            _categoryList = GetNode<VBoxContainer>("CategoryList");

            _title.Text = TranslationServer.Translate("mod_manager/filters");
            _reset.Text = TranslationServer.Translate("mod_manager/reset_filters");
            _statusCaption.Text = TranslationServer.Translate("mod_manager/filter_status");
            _categoryCaption.Text = TranslationServer.Translate("mod_manager/filter_categories");
            _groupToggle.Text = TranslationServer.Translate("mod_manager/group_by_category");
            _groupToggle.ButtonPressed = _state.GroupByCategory;

            _statusCaption.AddThemeColorOverride("font_color", UiTokens.Muted);
            _categoryCaption.AddThemeColorOverride("font_color", UiTokens.Muted);

            _reset.Pressed += OnResetPressed;
            _groupToggle.Toggled += OnGroupToggled;

            foreach (var status in Enum.GetValues<ModItemStatus>())
                AddStatusCheck(status);
            foreach (var category in ModCategoryNames.Ordered)
                AddCategoryCheck(category);
        }

        /// <summary>Refreshes the numbers in the checkbox captions.</summary>
        public void RefreshCounts(
            Dictionary<CategoryType, int> categoryCounts,
            Dictionary<ModItemStatus, int> statusCounts)
        {
            foreach (var pair in _statusBoxes)
            {
                var count = statusCounts.GetValueOrDefault(pair.Key);
                pair.Value.Text = $"{ModStatusNames.Translate(pair.Key)} ({count})";
            }

            foreach (var pair in _categoryBoxes)
            {
                var count = categoryCounts.GetValueOrDefault(pair.Key);
                pair.Value.Text = $"{ModCategoryNames.Translate(pair.Key)} ({count})";
            }
        }

        private void AddStatusCheck(ModItemStatus status)
        {
            var box = new CheckBox
            {
                Text = ModStatusNames.Translate(status),
                ButtonPressed = _state.Statuses.Contains(status),
            };
            box.Toggled += pressed => OnStatusToggled(status, pressed);
            _statusBoxes[status] = box;
            _statusList.AddChild(box);
        }

        private void AddCategoryCheck(CategoryType category)
        {
            var box = new CheckBox
            {
                Text = ModCategoryNames.Translate(category),
                ButtonPressed = _state.Categories.Contains(category),
            };
            box.Toggled += pressed => OnCategoryToggled(category, pressed);
            _categoryBoxes[category] = box;
            _categoryList.AddChild(box);
        }

        private void OnStatusToggled(ModItemStatus status, bool pressed)
        {
            if (pressed)
                _state.Statuses.Add(status);
            else
                _state.Statuses.Remove(status);

            Changed?.Invoke();
        }

        private void OnCategoryToggled(CategoryType category, bool pressed)
        {
            if (pressed)
                _state.Categories.Add(category);
            else
                _state.Categories.Remove(category);

            Changed?.Invoke();
        }

        private void OnGroupToggled(bool pressed)
        {
            _state.GroupByCategory = pressed;
            Changed?.Invoke();
        }

        private void OnResetPressed()
        {
            _state.Reset();

            foreach (var box in _statusBoxes.Values)
                box.SetPressedNoSignal(false);
            foreach (var box in _categoryBoxes.Values)
                box.SetPressedNoSignal(false);

            ResetRequested?.Invoke();
            Changed?.Invoke();
        }
    }
}
