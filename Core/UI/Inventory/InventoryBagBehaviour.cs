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

        /// <summary>
        /// Bound inventory data source. Setting this property immediately rebuilds
        /// the grid display via <see cref="Refresh"/>. The entire grid is
        /// reconstructed from the new list reference.
        /// </summary>
        public List<IItemContainer> List
        {
            get => list;
            set
            {
                list = value;
                Refresh();
            }
        }

        /// <summary>
        /// Provides the current <see cref="List"/> as the data collection for the
        /// <see cref="ColumnCellListWidget{TItem, TModel}"/> base.
        /// </summary>
        public override ICollection<IItemContainer> CreateModels()
        {
            return list;
        }
    }
}
