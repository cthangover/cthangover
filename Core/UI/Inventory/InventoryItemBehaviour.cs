using Cthangover.Core.Items;
using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Renders a single inventory slot: item icon from Model.Item.Sprite and
    /// stack count. Suppresses the count label for single items (clean display).
    /// Destruct calls QueueFree to fully remove the node — the ColumnCellListWidget
    /// rebuilds all items on refresh rather than recycling.
    /// </summary>
    public partial class InventoryItemBehaviour : ListItem<IItemContainer>
    {
        [Export] private TextureRect imgIcon;
        [Export] private Label txtCount;

        public override void Construct(IItemContainer container)
        {
            base.Construct(container);
            UpdateInfo();
        }

        public void UpdateInfo()
        {
            imgIcon.Texture = Model.Item.Sprite;
            txtCount.Text = Model.Count == 1 ? "" : Model.Count.ToString();
        }

        public override void Destruct()
        {
            QueueFree();
        }

    }
}
