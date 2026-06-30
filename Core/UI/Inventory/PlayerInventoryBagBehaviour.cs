using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists.Impls;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// The player's personal inventory bag. Subscribes to GameData.Instance.Runtime.
    /// Inventory.Change for auto-refresh — when the inventory mutates, the bag
    /// calls Refresh() to rebuild items. Overrides Show to update the content
    /// size from the ScrollContainer before displaying, ensuring the grid width
    /// matches the viewport. Accepts all GuiInput to prevent clicks from falling
    /// through to underlying scene objects. On _ExitTree, unsubscribes from the
    /// change event to prevent leaks.
    /// </summary>
    public partial class PlayerInventoryBagBehaviour : ColumnCellListWidget<InventoryItemBehaviour, IItemContainer>
    {
        /// <summary>
        /// Subscribes to inventory change events for auto-refresh, sets mouse
        /// filter to <c>Stop</c> to block click-through, and hides the bag
        /// initially.
        /// </summary>
        public override void _Ready()
        {
            GameData.Instance.Runtime.Inventory.Change += Refresh;
            if (Body == null)
                Set("body", this);
            MouseFilter = MouseFilterEnum.Stop;
            Hide();
        }

        /// <summary>
        /// Shows the inventory bag. Updates content size to match the
        /// <c>ScrollContainer</c> width and sets mouse filter to block
        /// click-through to the game world.
        /// </summary>
        public override void Show()
        {
            MouseFilter = MouseFilterEnum.Stop;
            UpdateContentSize();
            base.Show();
        }

        /// <summary>
        /// Consumes all GUI input to prevent events from reaching nodes behind
        /// the inventory panel.
        /// </summary>
        public override void _GuiInput(InputEvent @event)
        {
            AcceptEvent();
        }

        /// <summary>
        /// Unsubscribes from the inventory change event to prevent memory leaks
        /// when this node leaves the scene tree.
        /// </summary>
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

        /// <summary>
        /// Provides the player's inventory items from
        /// <see cref="GameData.Instance.Runtime.Inventory.Items"/> as the data
        /// source for the grid display.
        /// </summary>
        public override ICollection<IItemContainer> CreateModels()
        {
            return GameData.Instance.Runtime.Inventory.Items;
        }
    }
}
