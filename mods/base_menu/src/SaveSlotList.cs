using System;
using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Base.Lists.Impls;
namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Save slot grid layout with configurable column/row count and padding.
    /// Does NOT use the ListWidget/ColumnCellListWidget framework — manually
    /// calculates cell dimensions from the control size and instantiates
    /// SaveSlotItem prefabs in a flat grid. This is intentional: the save grid
    /// has a fixed visual size and doesn't need scrolling or dynamic content
    /// sizing. OnResize refreshes if slots were cleared (e.g. initial render
    /// when size wasn't known yet).
    /// </summary>
    public partial class SaveSlotList : Control
    {
        private ModScenePrefab _itemScene;
        private readonly List<SaveSlotItem> _items = new();

        /// <summary>
        /// Raised when any child <see cref="SaveSlotItem"/> is clicked, forwarding the save file
        /// name. A plain C# event because mod assemblies are compiled without the Godot source
        /// generators.
        /// </summary>
        public event Action<string> SlotPressed;

        /// <summary>Number of columns in the save slot grid. Editor-configurable.</summary>
        [Export]
        public int ColumnCount { get; set; } = 4;

        /// <summary>Number of rows in the save slot grid. Editor-configurable.</summary>
        [Export]
        public int RowCount { get; set; } = 3;

        /// <summary>Spacing between adjacent slots in pixels. Editor-configurable.</summary>
        [Export]
        public float SlotPadding { get; set; } = 8f;

        private List<SaveSlotInfo> _slotModels;

        /// <summary>Width kept free for the vertical scroll bar of the parent <see cref="ScrollContainer"/>.</summary>
        private const float ScrollBarReserve = 14f;

        public override void _Ready()
        {
            _itemScene = new ModScenePrefab("scenes/menu/save_slot_item.tscn");
            Resized += OnResized;
            if (GetParent() is Control viewport)
                viewport.Resized += OnResized;
        }

        /// <summary>
        /// Area one screen of slots occupies: the parent scroll viewport when
        /// the list scrolls (slots are unlimited), otherwise the list itself.
        /// </summary>
        private Vector2 ViewportSize()
            => GetParent() is ScrollContainer scroll
                ? new Vector2(scroll.Size.X - ScrollBarReserve, scroll.Size.Y)
                : Size;

        /// <summary>
        /// Stores the slot data models and triggers a full grid rebuild via <see cref="Refresh"/>.
        /// </summary>
        public void SetSlots(List<SaveSlotInfo> slots)
        {
            _slotModels = slots;
            Refresh();
        }

        private void ClearItems()
        {
            foreach (var item in _items)
            {
                if (GodotObject.IsInstanceValid(item))
                    item.QueueFree();
            }
            _items.Clear();
        }

        /// <summary>
        /// Destroys all existing <see cref="SaveSlotItem"/> children and rebuilds the grid
        /// from <c>_slotModels</c>. Calculates cell dimensions as (total - gaps) / column|row count,
        /// then positions each item using flat coordinates (no <c>GridContainer</c> dependency).
        /// Skips rendering if the control has zero size (e.g. called before first layout pass).
        /// </summary>
        public void Refresh()
        {
            ClearItems();

            if (_slotModels == null || _itemScene == null)
                return;

            var contentSize = ViewportSize();
            if (contentSize.X <= 0 || contentSize.Y <= 0)
                return;

            // One screen holds ColumnCount × RowCount slots; more rows extend
            // the list and the parent ScrollContainer scrolls.
            var cellWidth = (contentSize.X - (ColumnCount - 1) * SlotPadding) / ColumnCount;
            var cellHeight = (contentSize.Y - (RowCount - 1) * SlotPadding) / RowCount;
            var rows = Math.Max(1, (_slotModels.Count + ColumnCount - 1) / ColumnCount);
            CustomMinimumSize = new Vector2(0, rows * cellHeight + (rows - 1) * SlotPadding);

            for (int i = 0; i < _slotModels.Count; i++)
            {
                var model = _slotModels[i];

                var item = _itemScene.Instantiate<SaveSlotItem>();
                AddChild(item);

                var col = i % ColumnCount;
                var row = i / ColumnCount;

                item.Position = new Vector2(
                    col * (cellWidth + SlotPadding),
                    row * (cellHeight + SlotPadding)
                );
                item.Size = new Vector2(cellWidth, cellHeight);

                item.Construct(model);
                item.SlotPressed += OnItemSlotPressed;
                _items.Add(item);
            }
        }

        private void OnResized()
        {
            var view = ViewportSize();
            if (_slotModels != null && view.X > 0 && view.Y > 0
                && (_items.Count == 0 || Math.Abs(view.X - _laidOutFor.X) > 1 || Math.Abs(view.Y - _laidOutFor.Y) > 1))
            {
                _laidOutFor = view;
                Refresh();
            }
        }

        private Vector2 _laidOutFor;

        private void OnItemSlotPressed(string fileName)
        {
            SlotPressed?.Invoke(fileName);
        }
    }
}
