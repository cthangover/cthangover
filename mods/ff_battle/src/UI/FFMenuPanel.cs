using System.Collections.Generic;
using Cthangover.Core.UI;
using Godot;

namespace Cthangover.FFBattle.UI
{
    public class FFMenuEntry
    {
        public string Key;
        public string Label;
        public bool Enabled = true;
        public object Data;
    }

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

        public List<FFMenuEntry> Entries { get; private set; } = new();
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

        public event System.Action<int> OnItemSelected;
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

        public void HideMenu()
        {
            _visible = false;
            Visible = false;
            _cursorLabel.Visible = false;
        }

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
