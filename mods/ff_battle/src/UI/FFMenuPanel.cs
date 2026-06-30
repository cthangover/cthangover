using System.Collections.Generic;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Data object for a single menu entry. <see cref="Key"/> drives the action
    /// dispatch in <see cref="FFBattleCore.MatchMenuAction"/> (values like
    /// <c>"attack"</c>, <c>"action"</c>, <c>"item"</c>, <c>"back"</c>).
    /// <see cref="Data"/> carries arbitrary payload: an
    /// <see cref="ActionCharacter"/> for action entries or an
    /// <see cref="IItem"/> for item entries.
    /// </summary>
    public class FFMenuEntry
    {
        /// <summary>Dispatch key used by <see cref="FFBattleCore"/> to determine the next step.</summary>
        public string Key;
        /// <summary>Display label shown in the menu.</summary>
        public string Label;
        /// <summary>Whether this entry can be selected (greyed out if false).</summary>
        public bool Enabled = true;
        /// <summary>Payload data: <see cref="ActionCharacter"/> for actions, <see cref="IItem"/> for items.</summary>
        public object Data;
    }

    /// <summary>
    /// Vertical scrolling menu panel with a blinking cursor, keyboard navigation,
    /// and mouse hover/click support. Used as both the main battle menu and the
    /// action/item sub-menu (a separate instance). Each menu is populated via
    /// <see cref="ShowMenu"/> with a list of <see cref="FFMenuEntry"/> objects.
    /// Selection wraps around but skips disabled entries. Raises
    /// <see cref="OnItemSelected"/> on confirm, <see cref="OnCancelled"/> on
    /// right-click or Escape (handled by <see cref="FFBattleController"/>).
    /// </summary>
    public partial class FFMenuPanel : ModWidget
    {
        private ColorRect _background;
        private ColorRect _border;
        private Label _cursorLabel;
        private List<Label> _itemLabels = new();
        private Tween _cursorTween;
        private int _selectedIndex;
        private bool _visible;

        private const float ITEM_HEIGHT = 34f;
        private const float PADDING = 18f;
        private const float CURSOR_OFFSET_X = 6f;
        private const float CURSOR_OFFSET_Y = 7f;

        /// <summary>The entries currently displayed in the menu.</summary>
        public List<FFMenuEntry> Entries { get; private set; } = new();
        /// <summary>
        /// 0-based index of the highlighted entry. Setting it moves the
        /// cursor and clamps to the valid range.
        /// </summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                var clamped = Mathf.Clamp(value, 0, Entries.Count - 1);
                if (clamped == _selectedIndex)
                    return;
                _selectedIndex = clamped;
                MoveCursor(_selectedIndex);
            }
        }

        /// <summary>Raised when an entry is confirmed (Enter/Space or left click).</summary>
        public event System.Action<int> OnItemSelected;
        /// <summary>Raised when the menu is cancelled (right click, or Escape handled externally).</summary>
        public event System.Action OnCancelled;

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;

            _background = new ColorRect();
            _background.SetAnchorsPreset(LayoutPreset.FullRect);
            _background.Color = new Color(0.02f, 0.05f, 0.12f, 0.88f);
            _background.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_background);

            _border = new ColorRect();
            _border.SetAnchorsPreset(LayoutPreset.FullRect);
            _border.OffsetLeft = -3;
            _border.OffsetTop = -3;
            _border.OffsetRight = 3;
            _border.OffsetBottom = 3;
            _border.Color = new Color(0.3f, 0.5f, 0.9f, 0.9f);
            _border.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_border);

            _cursorLabel = new Label();
            _cursorLabel.Text = "►";
            _cursorLabel.AddThemeFontSizeOverride("font_size", 18);
            _cursorLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.7f, 1f));
            _cursorLabel.Visible = false;
            _cursorLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_cursorLabel);

            GuiInput += OnPanelGuiInput;
        }

        /// <summary>
        /// Renders the menu with the given entries. Creates labels, wires hover/click
        /// handlers, sizes the panel to fit, positions the cursor at index 0, and
        /// makes the panel visible.
        /// </summary>
        public void ShowMenu(List<FFMenuEntry> entries, string title = null)
        {
            ClearEntries();
            Entries = entries;

            float width = 220f;
            float totalHeight = PADDING * 2 + ITEM_HEIGHT * entries.Count;
            Size = new Vector2(width, totalHeight);

            for (int i = 0; i < entries.Count; i++)
            {
                var label = new Label();
                label.Text = entries[i].Label;
                label.AddThemeFontSizeOverride("font_size", 17);
                label.AddThemeColorOverride("font_color", entries[i].Enabled
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(0.5f, 0.5f, 0.5f, 1f));
                label.MouseFilter = MouseFilterEnum.Stop;
                label.Position = new Vector2(PADDING + 24, PADDING + i * ITEM_HEIGHT + CURSOR_OFFSET_Y);
                label.Size = new Vector2(width - PADDING * 2 - 24, ITEM_HEIGHT);
                label.ClipText = true;

                int idx = i;
                label.GuiInput += (evt) =>
                {
                    if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    {
                        if (entries[idx].Enabled)
                        {
                            _selectedIndex = idx;
                            MoveCursor(idx);
                            OnItemSelected?.Invoke(idx);
                        }
                    }
                };
                label.MouseEntered += () =>
                {
                    if (entries[idx].Enabled)
                    {
                        SelectedIndex = idx;
                    }
                };

                AddChild(label);
                _itemLabels.Add(label);
            }

            _selectedIndex = 0;
            MoveCursor(0);
            _cursorLabel.Visible = true;
            _visible = true;
            Visible = true;
        }

        /// <summary>Hides the menu and cursor. Does not clear entries — call <see cref="ShowMenu"/> to repopulate.</summary>
        public void HideMenu()
        {
            _visible = false;
            Visible = false;
            _cursorLabel.Visible = false;
        }

        /// <summary>Moves selection down, wrapping around and skipping disabled entries.</summary>
        public void SelectNext()
        {
            if (!_visible || Entries.Count == 0)
                return;

            var next = _selectedIndex;
            for (int i = 0; i < Entries.Count; i++)
            {
                next = (next + 1) % Entries.Count;
                if (Entries[next].Enabled)
                    break;
            }
            SelectedIndex = next;
        }

        /// <summary>Moves selection up, wrapping around and skipping disabled entries.</summary>
        public void SelectPrevious()
        {
            if (!_visible || Entries.Count == 0)
                return;

            var prev = _selectedIndex;
            for (int i = 0; i < Entries.Count; i++)
            {
                prev = (prev - 1 + Entries.Count) % Entries.Count;
                if (Entries[prev].Enabled)
                    break;
            }
            SelectedIndex = prev;
        }

        /// <summary>Invokes <see cref="OnItemSelected"/> for the currently highlighted entry if it is enabled.</summary>
        public void ConfirmSelection()
        {
            if (!_visible || Entries.Count == 0)
                return;

            if (_selectedIndex >= 0 && _selectedIndex < Entries.Count && Entries[_selectedIndex].Enabled)
                OnItemSelected?.Invoke(_selectedIndex);
        }

        private void MoveCursor(int index)
        {
            _cursorTween?.Kill();
            _cursorLabel.Position = new Vector2(CURSOR_OFFSET_X, PADDING + index * ITEM_HEIGHT + CURSOR_OFFSET_Y);

            _cursorTween = CreateTween();
            _cursorTween.SetLoops();
            _cursorTween.TweenProperty(_cursorLabel, "position:x", CURSOR_OFFSET_X + 3f, 0.3f);
            _cursorTween.TweenProperty(_cursorLabel, "position:x", CURSOR_OFFSET_X, 0.3f);
        }

        private void OnPanelGuiInput(InputEvent evt)
        {
            if (evt is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                OnCancelled?.Invoke();
            }
        }

        private void ClearEntries()
        {
            foreach (var label in _itemLabels)
                label.QueueFree();
            _itemLabels.Clear();
        }

        protected override void Destruct()
        {
            ClearEntries();
            _cursorTween?.Kill();
        }
    }
}
