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

        /// <summary>
        /// Binds the item model and renders the visual display via
        /// <see cref="UpdateInfo"/>.
        /// </summary>
        public override void Construct(IItemContainer container)
        {
            base.Construct(container);
            UpdateInfo();
        }

        /// <summary>
        /// Refreshes the icon from the model's sprite and the count label.
        /// For single items, the count is suppressed for a clean display.
        /// </summary>
        public void UpdateInfo()
        {
            imgIcon.Texture = Model.Item.Sprite;
            txtCount.Text = Model.Count == 1 ? "" : Model.Count.ToString();
        }

        /// <summary>
        /// Removes this cell from the scene tree via <c>QueueFree</c>.
        /// </summary>
        public override void Destruct()
        {
            QueueFree();
        }

    }
}
