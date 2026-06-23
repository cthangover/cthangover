using Cthangover.Core.Items;
using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
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
