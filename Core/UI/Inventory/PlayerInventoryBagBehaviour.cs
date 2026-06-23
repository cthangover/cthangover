using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists.Impls;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    public partial class PlayerInventoryBagBehaviour : ColumnCellListWidget<InventoryItemBehaviour, IItemContainer>
    {
        public override void _Ready()
        {
            GameData.Instance.Runtime.Inventory.Change += Refresh;
            if (Body == null)
                Set("body", this);
            MouseFilter = MouseFilterEnum.Stop;
            Hide();
        }

        public override void Show()
        {
            MouseFilter = MouseFilterEnum.Stop;
            UpdateContentSize();
            base.Show();
        }

        public override void _GuiInput(InputEvent @event)
        {
            AcceptEvent();
        }

        public override void _ExitTree()
        {
            GameData.Instance.Runtime.Inventory.Change -= Refresh;
        }

        private void UpdateContentSize()
        {
            if (Content == null)
                return;

            var scroll = GetNodeOrNull<ScrollContainer>("ScrollContainer");
            if (scroll != null)
            {
                Content.CustomMinimumSize = new Vector2(scroll.Size.X, 0);
                Content.Size = new Vector2(scroll.Size.X, Content.Size.Y);
            }
        }

        public override ICollection<IItemContainer> CreateModels()
        {
            return GameData.Instance.Runtime.Inventory.Items;
        }
    }
}
