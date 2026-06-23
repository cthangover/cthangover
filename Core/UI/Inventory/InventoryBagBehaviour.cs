using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.UI.Base.Lists.Impls;

namespace Cthangover.Core.UI.Inventory
{
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
