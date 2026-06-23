using System.Collections.Generic;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    public partial class SaveSlotList : Control
    {
        private PackedScene _itemScene;
        private readonly List<SaveSlotItem> _items = new();

        [Signal]
        public delegate void SlotPressedEventHandler(string fileName);

        [Export]
        public int ColumnCount { get; set; } = 4;

        [Export]
        public int RowCount { get; set; } = 3;

        [Export]
        public float SlotPadding { get; set; } = 8f;

        private List<SaveSlotInfo> _slotModels;

        public override void _Ready()
        {
            _itemScene = GD.Load<PackedScene>("res://scenes/SaveSlotItem.tscn");
        }

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

        public void Refresh()
        {
            ClearItems();

            if (_slotModels == null || _itemScene == null)
                return;

            var contentSize = Size;
            if (contentSize.X <= 0 || contentSize.Y <= 0)
                return;

            var cellWidth = (contentSize.X - (ColumnCount - 1) * SlotPadding) / ColumnCount;
            var cellHeight = (contentSize.Y - (RowCount - 1) * SlotPadding) / RowCount;

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

        private void OnItemSlotPressed(string fileName)
        {
            EmitSignal(SignalName.SlotPressed, fileName);
        }
    }
}
