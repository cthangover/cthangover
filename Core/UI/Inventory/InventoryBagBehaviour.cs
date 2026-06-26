using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.UI.Base.Lists.Impls;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Grid-based inventory display using ColumnCellListWidget. Binds an
    /// external List&lt;IItemContainer&gt;. The List property setter auto-refreshes
    /// the UI — intended for data-driven updates where the inventory source
    /// pushes a new list reference rather than items mutating in place.
    /// </summary>
    public partial class InventoryBagBehaviour : ColumnCellListWidget<InventoryItemBehaviour, IItemContainer>
    {
        private List<IItemContainer> list;
        public List<IItemContainer> List
        {
            get => list;
            set
            {
                list = value;
                Refresh();
            }
        }

        public override ICollection<IItemContainer> CreateModels()
        {
            return list;
        }
    }
}
